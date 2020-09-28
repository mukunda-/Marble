// Marbles
// (C) 2020 Mukunda Johnson
/////////////////////////////////////////////////////////////////////////////////////////
using System;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    //-----------------------------------------------------------------------------------
    // Encapsulates sprint (deep work) logic.
    public class Sprint
    {
        // For speeding things up to test out the system.
        public static double debugTimeScale = 1.0;

        bool running;
        bool completed = false;
        double SprintMinutes { get; set; }
        double RestMinutes { get; set; }
        DateTime StartTime { get; set; }
        
        // Called when the sprint is completed.
        public event EventHandler SprintCompleted;

        // Called when the sprint is started.
        public event EventHandler SprintStarted;

        //-------------------------------------------------------------------------------
        public enum Mode
        {
            Stopped,   // We haven't started yet OR the sprint was cancelled.
            Sprinting, // We are in a Deep Work segment.
            Resting,   // We are in a Rest segment.
            After      // We are in a post-rest segment (doesn't expire).
        }

        //-------------------------------------------------------------------------------
        // Returned by GetStatus.
        public struct Status
        {
            public Mode mode;
            // Seconds until the next block starts. Will start at 0 and go negative for
            //  the `After` mode.
            public double secondsRemaining;

            // How many seconds we are into the current block. Starts at 0 each phase
            //  shift.
            public double secondsInto;

            // How many seconds have elapsed total since the start of the sprint.
            public double totalElapsedSeconds;

            // How long the sprint period is.
            public double sprintMinutes;

            // How long the rest period is.
            public double restMinutes;
        }

        //-------------------------------------------------------------------------------
        // Start a new sprint.
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
        // Cancel a running sprint.
        public void Cancel()
        {
            if (!this.running) return;
            this.running = false;
        }

        //-------------------------------------------------------------------------------
        // Set the completed flag for this block and invoke the callback if it wasn't
        //  triggered yet.
        private void SetComplete()
        {
            if (this.completed) return;
            this.completed = true;
            SprintCompleted?.Invoke(this, null);
        }

        //-------------------------------------------------------------------------------
        // Update logic. Called periodically. Whenever it's called it calculates the
        //  status and returns it.
        public Status Update()
        {
            Status status;
            status.sprintMinutes = SprintMinutes;
            status.restMinutes   = RestMinutes;

            if (!running)
            {
                // We aren't running.
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
                    // We're in the Sprint block.
                    status.mode = Mode.Sprinting;
                    status.secondsRemaining = SprintMinutes * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed;
                }
                else if (timeElapsed < (SprintMinutes + RestMinutes) * 60.0)
                {
                    // We're in the Rest block.
                    SetComplete();
                    status.mode = Mode.Resting;
                    status.secondsRemaining = (SprintMinutes + RestMinutes) * 60.0 - timeElapsed;
                    status.secondsInto = timeElapsed - SprintMinutes * 60;
                }
                else
                {
                    // We're in the After block.

                    SetComplete(); // This is done here if the rest period is 0.
                    status.mode = Mode.After;
                    status.secondsRemaining = (SprintMinutes + RestMinutes) * 60 - timeElapsed;
                    status.secondsInto = timeElapsed - (SprintMinutes + RestMinutes) * 60;
                }
            }

            return status;
        }
    }
}
/////////////////////////////////////////////////////////////////////////////////////////
