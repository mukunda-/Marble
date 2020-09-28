// Marbles
// (C) 2020 Mukunda Johnson
/////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    //-----------------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        // What is the proper way to define something static like this?
        // This is the font to use for drawing the taskbar button badge.
        private static readonly Typeface ICON_TYPEFACE = new Typeface("Arial");

        // Reference to our App instance.
        private readonly App app;

        //-------------------------------------------------------------------------------
        public MainWindow()
        {
            app = Application.Current as App;
            InitializeComponent();

            app.settings.Loaded        += ApplySettings;
            ApplySettings(app.settings);

            app.sprint.SprintStarted   += OnSprintStart;
            app.sprint.SprintCompleted += OnSprintCompleted;
            app.PeriodicUpdate         += OnPeriodicUpdate;
        }

        //-------------------------------------------------------------------------------
        private void OnPeriodicUpdate()
        {
            UpdateDisplay();
        }

        //-------------------------------------------------------------------------------
        private void ApplySettings(Settings settings)
        {
            // Force refresh the icon since settings might cause it to look different.
            // Might not be necessary but just in case down the road...
            this.lastIconSerial = "";
            this.lastIconUpdate = DateTime.MinValue;
            this.Topmost = settings.fields.WindowAlwaysOnTop;
            UpdateMarblesToday(false);
        }

        //-------------------------------------------------------------------------------
        private void UpdateMarblesToday( bool increment )
        {
            var settings = App.Settings;
            bool save = false;
            string dateString = DateTime.Now.ToString("MM/dd/yyyy");
            if (dateString != settings.fields.DateToday)
            {
                settings.fields.DateToday = dateString;
                settings.fields.MarblesDoneToday = 0;
                save = true;
            }

            if (increment)
            {
                settings.fields.MarblesDoneToday++;
                save = true;
            }

            if (save)
            {
                settings.Save();
            }
        }

        //-------------------------------------------------------------------------------
        // Called when a sprint completes.
        private void OnSprintCompleted(object sender, EventArgs e)
        {
            UpdateMarblesToday(true);

            if (app.settings.fields.PopupWhenRestStarts)
            {
                this.WindowState = WindowState.Normal;
            }
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
            string squareColor = "";
            string textType = "";

            bool hideIcon = false;
            
            if (status.mode == Sprint.Mode.After && status.secondsInto < 10.0)
            {
                if (app.settings.fields.ShowYellowFlashAfterRest)
                {
                    if (status.secondsInto % 2 < 1.0)
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                    else
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TaskbarItemInfo.ProgressValue = 1;
                }
                else
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                }

                hideIcon = true;
            }
            else if (status.mode == Sprint.Mode.Sprinting)
            {
                textType = "numbers";
                squareColor = "#e41313";

                if (app.settings.fields.ColorTaskbarDuringSprint)
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
                    TaskbarItemInfo.ProgressValue = 1.0;
                }
                else
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                }

                if (!app.settings.fields.ShowSprintBadge)
                {
                    // Setting is disabled.
                    hideIcon = true;
                }

            }
            else if (status.mode == Sprint.Mode.Resting)
            {
                textType = "numbers";
                squareColor = "#56be22";
                if (app.settings.fields.ColorTaskbarDuringRest)
                {
                    if (status.secondsInto < 5.0)
                    {
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                    }
                    else
                    {
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    }
                    TaskbarItemInfo.ProgressValue = 1.0;
                }
                else
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                }

                if (!app.settings.fields.ShowRestBadge)
                {
                    // Setting is disabled.
                    hideIcon = true;
                }
            }
            else
            {
                // Stopped
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                hideIcon = true;
            }

            if (hideIcon)
            {
                this.lastIconSerial = "";
                TaskbarItemInfo.Overlay = null;
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
            }

            TaskbarItemInfo.Overlay = new DrawingImage(visual.Drawing);
        }


        //-------------------------------------------------------------------------------
        bool mouseWillClick = false;
        Point mouseDownPosition;
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.mouseWillClick = true;
                this.mouseDownPosition = e.GetPosition( this );
            }
            CaptureMouse();
        }

        //-------------------------------------------------------------------------------
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                if (e.ChangedButton == MouseButton.Left && !this.mouseWillClick) return;
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
                && this.mouseWillClick
                && (mousePosition - this.mouseDownPosition).LengthSquared > 5 * 5)
            {
                this.mouseWillClick = false;
                ReleaseMouseCapture();
                DragMove();
                return;
            }
        }

        //-------------------------------------------------------------------------------
        private void UpdateDisplay()
        {
            var status = app.sprint.Update();
            if (status.mode == Sprint.Mode.Stopped || status.mode == Sprint.Mode.After)
            {
                startSprintDialog.Visibility = Visibility.Visible;
                this.Background = (Brush)FindResource("WindowBackground");
                this.BorderBrush = (Brush)FindResource("WindowBorder");
                Application.Current.Resources["CurrentForeground"]
                    = Application.Current.Resources["WindowForeground"];
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

                Application.Current.Resources["CurrentForeground"]
                    = Application.Current.Resources["SprintForeground"];
                statusLabel.Content = $"Deep Work ({app.settings.fields.MarblesDoneToday + 1})";
                var seconds = Math.Ceiling(status.secondsRemaining);
                timerLabel.Content = $"{Math.Floor(seconds / 60)}:{seconds % 60:00}";
                this.Title = $"Marbles – Deep Work ({app.settings.fields.MarblesDoneToday + 1})";
            }
            else if (status.mode == Sprint.Mode.Resting)
            {
                startSprintDialog.Visibility = Visibility.Hidden;
                this.Background = (Brush)FindResource("RestBackground");
                this.BorderBrush = (Brush)FindResource("RestBorder");

                Application.Current.Resources["CurrentForeground"]
                    = Application.Current.Resources["RestForeground"];
                statusLabel.Content = $"Rest ({app.settings.fields.MarblesDoneToday + 1})";
                var seconds = Math.Ceiling(status.secondsRemaining);
                timerLabel.Content = $"{Math.Floor(seconds / 60)}:{seconds % 60:00}";
                this.Title = $"Marbles – Rest ({app.settings.fields.MarblesDoneToday + 1})";
            }
            UpdateWindowIcon(status);

            
            // TODO...
            //RenderTargetBitmap bmp = new RenderTargetBitmap(
            //    (int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            //bmp.Render(this);
        }

        //-------------------------------------------------------------------------------
        public void OnSprintStart(object sender, EventArgs e)
        {
            UpdateDisplay();
            if (app.settings.fields.MinimizeWhenSprintStarts)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        //-------------------------------------------------------------------------------
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                (bool valid, double sprint, double rest) =
                    (startSprintDialog.Content as StartSprintPage).GetSprintSettings();
                if (!valid) return;
                app.TryStartWork(sprint, rest);
            }
        }

        //-------------------------------------------------------------------------------
        private void PopulateContextMenu()
        {
            var status = app.sprint.Update();
            var menu = new ContextMenu();
            
            // Reset button, appears when a sprint is active.
            if (status.mode == Sprint.Mode.Sprinting
                || status.mode == Sprint.Mode.Resting)
            {
                var item = new MenuItem();
                
                item.Header = "_Reset";
                item.Click += (obj, e) =>
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Are you sure you want to reset?",
                        "Discard Time Block",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        app.sprint.Cancel();
                    }
                };
                menu.Items.Add(item);
                menu.Items.Add(new Separator());
            }

            // Settings button.
            {
                var item = new MenuItem();
                item.Header = "_Settings";
                item.Click += (obj, e) =>
                {
                    // Open settings.
                    app.settings.OpenEditor();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());

            // Exit button.
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
/////////////////////////////////////////////////////////////////////////////////////////
