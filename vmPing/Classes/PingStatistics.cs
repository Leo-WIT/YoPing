using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace vmPing.Classes
{
    public class PingStatistics : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private uint sent;
        public uint Sent
        {
            get => sent;
            set { sent = value; OnPropertyChanged(); }
        }

        private uint received;
        public uint Received
        {
            get => received;
            set { received = value; OnPropertyChanged(); }
        }

        private uint lost;
        public uint Lost
        {
            get => lost;
            set { lost = value; OnPropertyChanged(); }
        }

        private uint error;
        public uint Error
        {
            get => error;
            set { error = value; OnPropertyChanged(); }
        }

        private long minRtt = long.MaxValue;
        public long MinRtt
        {
            get => minRtt == long.MaxValue ? 0 : minRtt;
            set { minRtt = value; OnPropertyChanged(); }
        }

        private long maxRtt;
        public long MaxRtt
        {
            get => maxRtt;
            set { maxRtt = value; OnPropertyChanged(); }
        }

        private long totalRtt;
        public double AverageRtt => Received == 0 ? 0 : (double)totalRtt / Received;
        public double LossRate => Sent == 0 ? 0 : (double)(Sent - Received) * 100 / Sent;

        public void RecordSuccess(long roundTripTime)
        {
            if (roundTripTime < minRtt)
            {
                MinRtt = roundTripTime;
            }
            if (roundTripTime > maxRtt)
            {
                MaxRtt = roundTripTime;
            }
            totalRtt += roundTripTime;
            OnPropertyChanged(nameof(AverageRtt));
            OnPropertyChanged(nameof(LossRate));
        }

        public void Reset()
        {
            sent = received = lost = error = 0;
            minRtt = long.MaxValue;
            maxRtt = 0;
            totalRtt = 0;
            OnPropertyChanged(nameof(Sent));
            OnPropertyChanged(nameof(Received));
            OnPropertyChanged(nameof(Lost));
            OnPropertyChanged(nameof(Error));
            OnPropertyChanged(nameof(MinRtt));
            OnPropertyChanged(nameof(MaxRtt));
            OnPropertyChanged(nameof(AverageRtt));
            OnPropertyChanged(nameof(LossRate));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
