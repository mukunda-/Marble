using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    //-----------------------------------------------------------------------------------
    public class Sprint
    {
        // For speeding things up to test out the system.
        public static double debugTimeScale = 1.0;

        bool running;
        bool completed = false;
        double SprintMinutes { get; set; }
        double RestMinutes { get; set; }
        DateTime StartTime { get; set; }

        public event EventHandler SprintCompleted;
        public event EventHandler SprintStarted;

        //-------------------------------------------------------------------------------
        public enum Mode
        {
            Stopped,
            Sprinting,
            Resting,
            After
        }

        //-------------------------------------------------------------------------------
        public struct Status
        {
            public Mode mode;
            public double secondsRemaining;
            public double secondsInto;
            public double totalElapsedSeconds;
            public double sprintMinutes;
            public double restMinutes;
        }

        //-------------------------------------------------------------------------------
        public void Start(double sprintMinutes, double restMinutes)
        {
            SprintMinutes = sprintMinutes;
            RestMinutes   = restMinutes;
            StartTime     = DateTime.Now;
            running       = true;
            completed     = false;
            SprintStarted?.Invoke(this, null);
        }

        //-------------------------------------------------------------------------------
        public void Cancel()
        {
            if (!this.running) return;
            this.running = false;
        }

        //-------------------------------------------------------------------------------
        private void SetComplete()
        {
            if (this.completed) return;
            this.completed = true;
            SprintCompleted?.Invoke(this, null);
        }

        //-------------------------------------------------------------------------------
        public Status Update()
        {
            Status status;
            status.sprintMinutes = SprintMinutes;
            status.restMinutes   = RestMinutes;

            if (!running)
            {
                status.mode = Mode.Stopped;
                status.totalElapsedSeconds = 0.0;
                status.secondsRemaining = 0.0;
                status.secondsInto = 0.0;
            }
            else
            {
                double timeElapsed = (DateTime.Now - StartTime).TotalSeconds * debugTimeScale;
                status.totalElapsedSeconds = timeElapsed;

                if (timeElapsed < this.SprintMinutes * 60.0)
                {
                    status.mode = Mode.Sprinting;
                    status.secondsRemaining = SprintMinutes * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed;
                }
                else if (timeElapsed < (SprintMinutes + RestMinutes) * 60.0)
                {
                    SetComplete();
                    status.mode = Mode.Resting;
                    status.secondsRemaining = (SprintMinutes + RestMinutes) * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed - SprintMinutes * 60;
                }
                else
                {
                    SetComplete();
                    status.mode = Mode.After;
                    status.secondsRemaining = (SprintMinutes + RestMinutes) * 60 - timeElapsed;
                    status.secondsInto = timeElapsed - (SprintMinutes + RestMinutes) * 60;
                }
            }

            return status;
        }
    }
}
