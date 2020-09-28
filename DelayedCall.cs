// Marbles
// (C) 2020 Mukunda Johnson
/////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Threading.Tasks;
using System.Windows;

/////////////////////////////////////////////////////////////////////////////////////////
namespace Marbles
{
    //-----------------------------------------------------------------------------------
    // This handles making delayed calls on a function.
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
                return;
            }

            int mySerial = serial;
            Task.Delay(delayMs).ContinueWith(t =>
            {
                if (mySerial == serial)
                {
                    // The app might exit before this triggers. Not really sure how the
                    //  heck the system manages these threads, but it scary.
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
                else
                {
                    // This call was cancelled.
                }
            });
        }
    }
}
/////////////////////////////////////////////////////////////////////////////////////////
