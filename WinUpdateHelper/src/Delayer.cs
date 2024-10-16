using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace WinUpdateHelper
{
    public class Delayer<T>
    {
        private static int Coutter = 0;
        private static Dictionary<Action<T>, int> dic=new Dictionary<Action<T>, int>();
        private static Dictionary<int, DispatcherTimer> keyDic = new Dictionary<int, DispatcherTimer>();
        private static Dictionary<int, T> dataDic = new Dictionary<int, T>();
        public static int Call(Action<T> action,T data, float second)
        {
            var key = 0;
            DispatcherTimer timer;
            if (dic.TryGetValue(action, out key))
            {
                timer = keyDic[key];
                timer.Stop();
            }
            else
            {
                key = Coutter++;
                timer = new DispatcherTimer();
                timer.Tick += Timer_Tick;
                dic.Add(action, key);
                keyDic.Add(key, timer);
            }

            dataDic[key] = data;
            timer.Tag = action;
            timer.Interval = TimeSpan.FromSeconds(second);
           
            timer.Start();

            return key;
        }

        public static void ClearAll()
        {
            var list = dic.Values.ToList();
            foreach (var item in list)
            {
                Stop(item);
            }
        }

        public static bool Stop(int key)
        {
            Action<T> result=null;
            foreach (var keyValuePair in dic)
            {
                if (keyValuePair.Value == key)
                {
                    result = keyValuePair.Key;
                    break;
                    
                }
            }

            return Stop(result);
        }
        public static bool Stop(Action<T> action)
        {
            if (action == null)
            {
                return false;
            }

            var key = 0;
            if (dic.TryGetValue(action, out key)==false)
            {
                return false;
            }
            dic.Remove(action);

            var timer = keyDic[key];
            timer.Stop();

            keyDic.Remove(key);
            dataDic.Remove(key);

            return true;
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;

            var action = timer.Tag as Action<T>;

            var key = dic[action];

            var data = dataDic[key];

            Stop(action);

            //Action a = () => { action(data); };
            // Application.Current.Dispatcher.BeginInvoke(a);

            action(data);
        }
    }
}