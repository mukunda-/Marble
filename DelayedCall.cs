using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
                return;
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
}
