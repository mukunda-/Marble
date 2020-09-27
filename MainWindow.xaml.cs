// Marble
// (C) 2020 Mukunda Johnson
//
// Copyright <YEAR> <COPYRIGHT HOLDER>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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

///////////////////////////////////////////////////////////////////////////////
namespace Marble
{
    //-------------------------------------------------------------------------
    public class Sprint {
        // For speeding things up to test out the system.
        public static double debugTimeScale = 1.0;

        bool running;
        double sprintMinutes { get; set; }
        double restMinutes { get; set; }
        DateTime startTime { get; set; }

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
            this.sprintMinutes = sprintMinutes;
            this.restMinutes = restMinutes;
            this.startTime = DateTime.Now;
            this.running = true;
        }

        public void Cancel()
        {
            if (!this.running) return;
            this.running = false;
        }

        public Status GetStatus()
        {
            Status status;
            status.sprintMinutes = this.sprintMinutes;
            status.restMinutes = this.restMinutes;

            if (!running)
            {
                status.mode = Mode.Stopped;
                status.totalElapsedSeconds = 0.0;
                status.secondsRemaining = 0.0;
                status.secondsInto = 0.0;
            }
            else
            {
                double timeElapsed = (DateTime.Now - this.startTime).TotalSeconds * debugTimeScale;
                status.totalElapsedSeconds = timeElapsed;

                if (timeElapsed < this.sprintMinutes * 60.0)
                {
                    status.mode = Mode.Sprinting;
                    status.secondsRemaining = this.sprintMinutes * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed;
                }
                else if (timeElapsed < (this.sprintMinutes + this.restMinutes) * 60.0)
                {
                    status.mode = Mode.Resting;
                    status.secondsRemaining = (this.sprintMinutes + this.restMinutes) * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed - this.sprintMinutes * 60;
                }
                else
                {
                    status.mode = Mode.After;
                    status.secondsRemaining = (this.sprintMinutes + this.restMinutes) * 60 - timeElapsed;
                    status.secondsInto = timeElapsed - (this.sprintMinutes + this.restMinutes) * 60;
                }
            }

            return status;
        }
    }

    //-------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        byte[] pixels = new byte[32 * 4 * 32];
        // What is the proper way to define something static like this?
        private static readonly Typeface ICON_TYPEFACE = new Typeface("Arial");

        Sprint sprint = new Sprint();
        
        private void ClickTest() {

        }

        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += Refresh;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
            dispatcherTimer.Start();

        }

        private void TimerTest(object sender, EventArgs e)
        {

            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
            this.TaskbarItemInfo.ProgressValue = 1.0 - this.TaskbarItemInfo.ProgressValue;
            
        }

        private void Refresh(object sender, EventArgs e)
        {
            //var random = new Random();
            //var pixels = new byte[32 * 4 * 32];
            //random.NextBytes(pixels);
            //this.Icon = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 32 * 4);
            

            UpdateDisplay();
            
            
        }


        private DateTime lastIconUpdate;
        private string lastIconSerial = "";

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
                    null,//new Pen((SolidColorBrush)(new BrushConverter().ConvertFrom("#fff")), 2),
                    new Rect(0, 0, 16, 16),
                    0, 0);

                if (textType == "numbers")
                {

                    var textBrush = brushConverter.ConvertFrom("#fff") as SolidColorBrush;
                    /*
                    for (int i = -1; i <= 1; i++)
                    {
                        var number = currentNumber + i;
                        if (number < 0 || number > maxNumber) continue;

                        FormattedText text = new FormattedText(
                            number.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, ICON_TYPEFACE,
                            number < 10 ? 24.0 : 18.0, textBrush, ppd);

                        text.SetFontWeight(FontWeights.Bold);

                        drawingContext.DrawText(text, new Point(
                                    16 + (number - remaining) * spacing - text.Width / 2,
                                    16 - text.Height / 2));
                    }*/
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
                    /*
                    drawingContext.DrawRoundedRectangle(
                        brushConverter.ConvertFrom("#aaa") as SolidColorBrush,
                        null,//new Pen((SolidColorBrush)(new BrushConverter().ConvertFrom("#fff")), 2),
                        new Rect(8, 8, 16, 16),
                        0, 0);*/
                }
                else if (textType == "none")
                {

                }
            }

            this.TaskbarItemInfo.Overlay = new DrawingImage(visual.Drawing);
            //this.Icon = 
        }


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

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                if (e.ChangedButton == MouseButton.Left && !mouseWillClick) return;
                OnClick(sender, e);
            }
        }

        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            var status = sprint.GetStatus();
            if (status.mode == Sprint.Mode.Stopped)
            {
            }
        }

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
                this.Title = "Marble";
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
                this.Title = "Marble – Deep Work";
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
                this.Title = "Marble – Rest";
            }
            UpdateWindowIcon(status);


            RenderTargetBitmap bmp = new RenderTargetBitmap(
                (int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(this);
            

        }

        public void StartWork(double sprintMinutes, double restMinutes)
        {
            var mode = this.sprint.GetStatus().mode;
            if (mode != Sprint.Mode.Stopped && mode != Sprint.Mode.After) return;
            this.sprint.Start(sprintMinutes, restMinutes);
            UpdateDisplay();
        }

        public void TryStartWork(double sprint, double rest) {
            // Only start if we aren't started already.
            var mode = this.sprint.GetStatus().mode;
            if (mode != Sprint.Mode.Stopped && mode != Sprint.Mode.After) return;
            StartWork(sprint, rest);
        }

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

        private void PopulateContextMenu()
        {
            var status = this.sprint.GetStatus();
            var menu = new ContextMenu();
            

            if( status.mode == Sprint.Mode.Sprinting || status.mode == Sprint.Mode.Resting )
            {
                var item = new MenuItem();
                
                item.Header = "Reset";
                item.Click += (obj, e) =>
                {
                    MessageBoxResult result = MessageBox.Show("Are you sure you want to reset?", "Reset Marble", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        this.sprint.Cancel();
                    }
                };
                menu.Items.Add(item);
            }
            {
                var item = new MenuItem();
                item.Header = "Settings";
                item.Click += (obj, e) =>
                {
                    // Open settings.
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            {
                var item = new MenuItem();
                item.Header = "Exit";
                item.Click += (obj, e) =>
                {
                    Application.Current.Shutdown();
                };
                menu.Items.Add(item);
            }
            
            this.ContextMenu = menu;
        }

        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            PopulateContextMenu();
        }
    }
}
