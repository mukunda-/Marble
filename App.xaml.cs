using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Marbles
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public readonly Settings settings = new Settings();
        public readonly Sprint sprint     = new Sprint();
        public delegate void EmptyEventHandler();
        public event EmptyEventHandler PeriodicUpdate;


        public static Settings Settings
        {
            get
            {
                return App.Cur.settings;
            }
        }

        public static App Cur
        {
            get
            {
                return Application.Current as App;
            }
        }

        public void OnStartup(object sender, EventArgs e)
        {
            this.settings.Loaded += OnSettingsLoaded;
            OnSettingsLoaded(this.settings);

            System.Windows.Threading.DispatcherTimer dispatcherTimer
                = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += OnPeriodicUpdate;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
            dispatcherTimer.Start();
        }

        public void OnExit(object sender, EventArgs e)
        {
            this.settings.Save();
        }

        //-------------------------------------------------------------------------------
        private void OnPeriodicUpdate(object sender, EventArgs e)
        {
            this.sprint.Update();
            PeriodicUpdate?.Invoke();
        }

        //-------------------------------------------------------------------------------
        private void OnSettingsLoaded(Settings settings)
        {
            
        }

        //-------------------------------------------------------------------------------
        public void StartWork(double sprintMinutes, double restMinutes)
        {
            this.sprint.Start(sprintMinutes, restMinutes);
        }

        //-------------------------------------------------------------------------------
        public void TryStartWork(double sprint, double rest)
        {
            // Only start if we aren't started already.
            var mode = this.sprint.Update().mode;
            if (mode != Sprint.Mode.Stopped && mode != Sprint.Mode.After) return;
            StartWork(sprint, rest);
        }
    }
}
