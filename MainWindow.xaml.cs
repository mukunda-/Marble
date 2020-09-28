// Marbles
// (C) 2020 Mukunda Johnson
//
// See LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;

using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    public class DelayedCall
    {
        public delegate void Callback();

        public int serial;

        public void Call(Callback callback, int delayMs = 0)
        {
            serial++;
            if (delayMs == 0)
            {
                callback();
            }

            int mySerial = serial;
            Task.Delay(delayMs).ContinueWith(t =>
            {
                if (mySerial == serial)
                {
                    Application.Current.Dispatcher.Invoke(callback);
                }
                else
                {
                    // This call was cancelled.
                }
            });
        }
    }
    //-----------------------------------------------------------------------------------
    public class Settings
    {
        private readonly string name = "settings";
        private readonly System.IO.FileSystemWatcher settingsWatcher;

        // To ignore reloading configuration when the app is changing the settings file.
        // Also used to prevent some "bounce trigger" issues that the filesystem watcher
        //  has.
        private DateTime ignoreFileChangesAt = DateTime.MinValue;

        public event EventHandler Loaded;

        DelayedCall loadCaller;

        public class Fields 
        {
            // Using strings for these two so it keeps the text boxes set as is.
            public string SprintTime { get; set; } = "25";
            public string RestTime { get; set; } = "5";

            public int MarblesDoneToday { get; set; } = 0;
            public string DateToday { get; set; } = "";
            public bool ShowSprintBadge { get; set; } = true;
            public bool ShowRestBadge { get; set; } = true;
            public bool MinimizeWhenSprintStarts { get; set; } = false;
            public bool PopupWhenRestStarts { get; set; } = false;
            public bool ColorTaskbarDuringSprint { get; set; } = true;
            public bool ColorTaskbarDuringRest { get; set; } = true;
            public bool ShowYellowFlashAfterRest { get; set; } = true;
            public bool WindowAlwaysOnTop { get; set; } = false;
        };

        public Fields fields = null;
        private Task queueLoadTask = null;

        //-------------------------------------------------------------------------------
        public Settings()
        {
            loadCaller = new DelayedCall();
            System.IO.Directory.CreateDirectory(AppDataFolder);
            Load();

            settingsWatcher = new System.IO.FileSystemWatcher
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite,
                Path         = AppDataFolder,
                Filter       = name + ".json"
            };
            settingsWatcher.Changed += (sender, e) =>
            {
                if (e.ChangeType != System.IO.WatcherChangeTypes.Changed) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if ((DateTime.Now - ignoreFileChangesAt).Milliseconds < 100)
                    {
                        return;
                    }
                    ignoreFileChangesAt = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine("Reloading settings.");
                    QueueLoad(250);
                });
            };
            settingsWatcher.EnableRaisingEvents = true;
        }
        
        public static string AppDataFolder
        {
            get
            {
                return System.IO.Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData), "Marbles");
            }
        }

        //-------------------------------------------------------------------------------
        public string SettingsFilePath
        {
            get
            {
                return System.IO.Path.Combine(AppDataFolder, this.name + ".json");
            }
        }

        //-------------------------------------------------------------------------------
        public void OpenEditor()
        {
            Save();
            System.Diagnostics.Process.Start(SettingsFilePath);
        }

        public void QueueLoad( int delayMs = 100 )
        {
            loadCaller.Call(Load, delayMs);
        }

        //-------------------------------------------------------------------------------
        public void Load()
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = false,
                };

                try
                {
                    string json;
                    try
                    {
                        json = System.IO.File.ReadAllText(SettingsFilePath);
                    }
                    catch (IOException e)
                    {
                        // IO error (like they are still writing the file). Wait and try again.
                        QueueLoad(1000);
                        return;
                    }
                    this.fields = System.Text.Json.JsonSerializer.Deserialize<Fields>(json, options);
                }
                catch
                {
                    
                }

                if (this.fields == null) this.fields = new Fields();

                // Sanitize.
                if (this.fields.MarblesDoneToday < 0) this.fields.MarblesDoneToday = 0;
            }
            else
            {
                // Will use default values.
                this.fields = new Fields();
                Save();
            }

            EventHandler handler = Loaded;
            handler?.Invoke(this, null);
        }

        //-------------------------------------------------------------------------------
        public void Save()
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            };
            string text = System.Text.Json.JsonSerializer.Serialize(fields, options);

            ignoreFileChangesAt = DateTime.Now;
            try
            {
                System.IO.File.WriteAllText(SettingsFilePath, text);
            }
            catch (IOException e)
            {
                // Can't save settings. Oh well. Not critical.
            }
        }
    }

    //-----------------------------------------------------------------------------------
    public class Sprint {
        // For speeding things up to test out the system.
        public static double debugTimeScale = 1.0;

        bool running;
        bool completed = false;
        double SprintMinutes { get; set; }
        double RestMinutes { get; set; }
        DateTime StartTime { get; set; }

        public event EventHandler SprintCompleted;

        public enum Mode {
            Stopped,
            Sprinting,
            Resting,
            After
        }

        public struct Status {
            public Mode mode;
            public double secondsRemaining;
            public double secondsInto;
            public double totalElapsedSeconds;
            public double sprintMinutes;
            public double restMinutes;
        }

        public void Start( double sprintMinutes, double restMinutes)
        {
            this.SprintMinutes = sprintMinutes;
            this.RestMinutes = restMinutes;
            this.StartTime = DateTime.Now;
            this.running = true;
            this.completed = false;
        }

        public void Cancel()
        {
            if (!this.running) return;
            this.running = false;
        }

        private void SetComplete()
        {
            if (this.completed) return;
            this.completed = true;
            SprintCompleted?.Invoke(this, null);
        }

        public Status GetStatus()
        {
            Status status;
            status.sprintMinutes = this.SprintMinutes;
            status.restMinutes = this.RestMinutes;

            if (!running)
            {
                status.mode = Mode.Stopped;
                status.totalElapsedSeconds = 0.0;
                status.secondsRemaining = 0.0;
                status.secondsInto = 0.0;
            }
            else
            {
                double timeElapsed = (DateTime.Now - this.StartTime).TotalSeconds * debugTimeScale;
                status.totalElapsedSeconds = timeElapsed;

                if (timeElapsed < this.SprintMinutes * 60.0)
                {
                    status.mode = Mode.Sprinting;
                    status.secondsRemaining = this.SprintMinutes * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed;
                }
                else if (timeElapsed < (this.SprintMinutes + this.RestMinutes) * 60.0)
                {
                    SetComplete();
                    status.mode = Mode.Resting;
                    status.secondsRemaining = (this.SprintMinutes + this.RestMinutes) * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed - this.SprintMinutes * 60;
                }
                else
                {
                    SetComplete();
                    status.mode = Mode.After;
                    status.secondsRemaining = (this.SprintMinutes + this.RestMinutes) * 60 - timeElapsed;
                    status.secondsInto = timeElapsed - (this.SprintMinutes + this.RestMinutes) * 60;
                }
            }

            return status;
        }
    }

    //-----------------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        byte[] pixels = new byte[32 * 4 * 32];
        // What is the proper way to define something static like this?
        private static readonly Typeface ICON_TYPEFACE = new Typeface("Arial");

        Sprint sprint = new Sprint();
        Settings settings;

        //-------------------------------------------------------------------------------
        public MainWindow()
        {
            InitializeComponent();

            this.settings = new Settings();
            this.settings.Loaded += OnSettingsLoaded;
            OnSettingsLoaded(null, null);
            this.sprint.SprintCompleted += OnSprintCompleted;

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += OnPeriodicRefresh;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
            dispatcherTimer.Start();
        }

        //-------------------------------------------------------------------------------
        private void UpdateMarblesToday( bool increment )
        {
            bool save = false;
            string dateString = DateTime.Now.ToString("MM/dd/yyyy");
            if (dateString != this.settings.fields.DateToday)
            {
                this.settings.fields.DateToday = dateString;
                this.settings.fields.MarblesDoneToday = 0;
                save = true;
            }

            if (increment)
            {
                this.settings.fields.MarblesDoneToday++;
            }

            if (save)
            {
                if (this.settings.fields.MarblesDoneToday == 0)
                {
                    statusLabel.Content = "Press enter to start.";
                }
                else
                {
                    var m = this.settings.fields.MarblesDoneToday;
                    statusLabel.Content = $"{m} marble{(m == 1 ? "" : "s")} done.";
                }

                this.settings.Save();
            }
        }

        //-------------------------------------------------------------------------------
        private void OnSettingsLoaded(object sender, EventArgs e)
        {
            //(startSprintDialog.Content as StartSprintPage).SetSprintSettings(
            //    this.settings.fields.SprintTime, this.settings.fields.RestTime);
            this.lastIconSerial = "";
            this.lastIconUpdate = DateTime.MinValue;
            this.Topmost = this.settings.fields.WindowAlwaysOnTop;

            UpdateMarblesToday(false);
        }

        private void OnSprintCompleted(object sender, EventArgs e)
        {
            UpdateMarblesToday(true);
        }

        //-------------------------------------------------------------------------------
        private void OnClosing(object sender, EventArgs e)
        {
            this.settings.Save();
        }

        //-------------------------------------------------------------------------------
        private void TimerTest(object sender, EventArgs e)
        {

            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
            this.TaskbarItemInfo.ProgressValue = 1.0 - this.TaskbarItemInfo.ProgressValue;
            
        }

        //-------------------------------------------------------------------------------
        private void OnPeriodicRefresh(object sender, EventArgs e)
        {
            UpdateDisplay();
        }


        private DateTime lastIconUpdate;
        private string lastIconSerial = "";

        //-------------------------------------------------------------------------------
        private void UpdateWindowIcon(Sprint.Status status)
        {
            // Throttle only every 0.5 seconds
            if ((DateTime.Now - this.lastIconUpdate).TotalSeconds < 0.5)
            {
                return;
            }
            this.lastIconUpdate = DateTime.Now;
            

            SolidColorBrush iconBrush;
            string squareColor;
            string textType;
            int maxNumber = 0;
            
            if (status.mode == Sprint.Mode.After && status.secondsInto < 10.0)
            {
                textType = "none";
                squareColor = "#f1e914";
                if (status.secondsInto % 2 < 1.0)
                    this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                else
                    this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                this.TaskbarItemInfo.ProgressValue = 1;

                this.TaskbarItemInfo.Overlay = null;
                return;
            }
            else if (status.mode == Sprint.Mode.Sprinting)
            {
                textType = "numbers";
                squareColor = "#e41313";
                this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
                this.TaskbarItemInfo.ProgressValue = 1.0;
                maxNumber = (int)Math.Floor(status.sprintMinutes);
            }
            else if (status.mode == Sprint.Mode.Resting)
            {
                textType = "numbers";
                squareColor = "#56be22";
                if (status.secondsInto < 5.0)
                {
                    this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                }
                else
                {
                    this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                }
                this.TaskbarItemInfo.ProgressValue = 1.0;
                maxNumber = (int)Math.Floor(status.restMinutes);
            }
            else
            {
                // Stopped
                textType = "dot";
                squareColor = "#222";
                this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                this.TaskbarItemInfo.Overlay = null;
                return;
            }

            double remaining = status.secondsRemaining / 60.0;
            var currentNumber = Math.Ceiling(remaining);

            string serial = textType + "|" + squareColor + "|" + (textType == "numbers" ? currentNumber.ToString() : "0");

            if (serial == this.lastIconSerial) return; // Not dirty.
            this.lastIconSerial = serial;

            var brushConverter = new BrushConverter();
            iconBrush = brushConverter.ConvertFrom(squareColor) as SolidColorBrush;

            var visual = new DrawingVisual();
            
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, 16, 16), 0, 0));
                
                drawingContext.DrawRoundedRectangle(
                    iconBrush,
                    null,
                    new Rect(0, 0, 16, 16),
                    0, 0);

                if (textType == "numbers")
                {

                    var textBrush = brushConverter.ConvertFrom("#fff") as SolidColorBrush;

                    FormattedText text = new FormattedText(
                            currentNumber.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, ICON_TYPEFACE,
                            currentNumber < 10 ? 14.0 : 11.0, textBrush, ppd);

                    text.SetFontWeight(FontWeights.Bold);

                    drawingContext.DrawText(text, new Point(
                                8 - text.Width / 2,
                                8 - text.Height / 2));

                }
                else if (textType == "dot")
                {
                    // Unused.
                }
                else if (textType == "none")
                {

                }
            }

            this.TaskbarItemInfo.Overlay = new DrawingImage(visual.Drawing);
        }


        //-------------------------------------------------------------------------------
        bool mouseWillClick = false;
        Point mouseDownPosition;
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                mouseWillClick = true;
                mouseDownPosition = e.GetPosition( this );
            }
            CaptureMouse();
        }

        //-------------------------------------------------------------------------------
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                if (e.ChangedButton == MouseButton.Left && !mouseWillClick) return;
                OnClick(sender, e);
            }
        }

        //-------------------------------------------------------------------------------
        private void OnClick(object sender, MouseButtonEventArgs e)
        {

        }

        //-------------------------------------------------------------------------------
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePosition = e.GetPosition(this);
            if (e.LeftButton ==MouseButtonState.Pressed
                && mouseWillClick
                && (mousePosition - mouseDownPosition).LengthSquared > 5 * 5)
            {
                mouseWillClick = false;
                ReleaseMouseCapture();
                DragMove();
                return;
            }
        }

        //-------------------------------------------------------------------------------
        private void UpdateDisplay()
        {
            var status = sprint.GetStatus();
            if (status.mode == Sprint.Mode.Stopped || status.mode == Sprint.Mode.After)
            {
                startSprintDialog.Visibility = Visibility.Visible;
                this.Background = (Brush)FindResource("WindowBackground");
                this.BorderBrush = (Brush)FindResource("WindowBorder");
                Application.Current.Resources["CurrentForeground"] = Application.Current.Resources["WindowForeground"];
                this.Foreground = (Brush)FindResource("WindowForeground");
                statusLabel.Content = "Ready";
                timerLabel.Content = "";
                this.Title = "Marbles";
            }
            else if (status.mode == Sprint.Mode.Sprinting)
            {
                startSprintDialog.Visibility = Visibility.Hidden;
                this.Background = (Brush)FindResource("SprintBackground");
                this.BorderBrush = (Brush)FindResource("SprintBorder");

                Application.Current.Resources["CurrentForeground"] = Application.Current.Resources["SprintForeground"];
                statusLabel.Content = "Deep Work";
                var seconds = Math.Ceiling(status.secondsRemaining);
                timerLabel.Content = $"{Math.Floor(seconds / 60)}:{seconds % 60:00}";
                this.Title = "Marbles – Deep Work";
            }
            else if (status.mode == Sprint.Mode.Resting)
            {
                startSprintDialog.Visibility = Visibility.Hidden;
                this.Background = (Brush)FindResource("RestBackground");
                this.BorderBrush = (Brush)FindResource("RestBorder");

                Application.Current.Resources["CurrentForeground"] = Application.Current.Resources["RestForeground"];
                statusLabel.Content = "Rest";
                var seconds = Math.Ceiling(status.secondsRemaining);
                timerLabel.Content = $"{Math.Floor(seconds / 60)}:{seconds % 60:00}";
                this.Title = "Marbles – Rest";
            }
            UpdateWindowIcon(status);


            RenderTargetBitmap bmp = new RenderTargetBitmap(
                (int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            

        }

        //-------------------------------------------------------------------------------
        public void StartWork(double sprintMinutes, double restMinutes)
        {
            this.sprint.Start(sprintMinutes, restMinutes);
            UpdateDisplay();

            if (this.settings.fields.MinimizeWhenSprintStarts)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        //-------------------------------------------------------------------------------
        public void TryStartWork(double sprint, double rest) {
            // Only start if we aren't started already.
            var mode = this.sprint.GetStatus().mode;
            if (mode != Sprint.Mode.Stopped && mode != Sprint.Mode.After) return;
            StartWork(sprint, rest);
        }

        //-------------------------------------------------------------------------------
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                (bool valid, double sprint, double rest) =
                    (startSprintDialog.Content as StartSprintPage).GetSprintSettings();
                if (!valid) return;
                TryStartWork(sprint, rest);
            }
        }

        //-------------------------------------------------------------------------------
        private void PopulateContextMenu()
        {
            var status = this.sprint.GetStatus();
            var menu = new ContextMenu();
            

            if( status.mode == Sprint.Mode.Sprinting || status.mode == Sprint.Mode.Resting )
            {
                var item = new MenuItem();
                
                item.Header = "_Reset";
                item.Click += (obj, e) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to reset?", "Reset Marble", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        this.sprint.Cancel();
                    }
                };
                menu.Items.Add(item);
                menu.Items.Add(new Separator());
            }
            {
                var item = new MenuItem();
                item.Header = "_Settings";
                item.Click += (obj, e) =>
                {
                    // Open settings.
                    this.settings.OpenEditor();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            {
                var item = new MenuItem();
                item.Header = "E_xit";
                item.Click += (obj, e) =>
                {
                    Application.Current.Shutdown();
                };
                menu.Items.Add(item);
            }
            
            this.ContextMenu = menu;
        }

        //-------------------------------------------------------------------------------
        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            PopulateContextMenu();
        }
    }
}
