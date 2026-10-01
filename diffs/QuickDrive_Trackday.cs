// Changes made to QuickDrive_Trackday inside Content Manager.exe:

namespace AcManager.Pages.Drive
{
    public class QuickDrive_Trackday : UserControl, IQuickDriveModeControl, ITabCanBePinned, IComponentConnector
    {
        public class ViewModel : QuickDrive_Race.ViewModel
        {
            protected new class SaveableData : QuickDrive_Race.ViewModel.SaveableData
            {
                public double SpeedLimit;
                // Added:
                public int TrackdayDuration;
            }

            private double _speedLimit;
            // Added:
            private int _trackdayDuration;

            // Added Property:
            public int TrackdayDuration
            {
                get
                {
                    return _trackdayDuration;
                }
                set
                {
                    value = MathUtils.Clamp(value, 0, 180);
                    if (_trackdayDuration != value)
                    {
                        _trackdayDuration = value;
                        ((NotifyPropertyChanged)this).OnPropertyChanged("TrackdayDuration");
                        base.SaveLater();
                    }
                }
            }

            public ViewModel(bool initialize = true) : base(initialize)
            {
                // Added: default duration is 10 minutes
                _trackdayDuration = 10;
            }

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result)
            {
                base.Save(result);
                SaveableData saveableData = (SaveableData)result;
                saveableData.SpeedLimit = SpeedLimit;
                // Added:
                saveableData.TrackdayDuration = TrackdayDuration;
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o)
            {
                base.Load(o);
                SaveableData saveableData = (SaveableData)o;
                SpeedLimit = saveableData.SpeedLimit;
                // Added:
                TrackdayDuration = saveableData.TrackdayDuration;
            }

            protected override BaseModeProperties GetModeProperties(IEnumerable<AiCar> botCars)
            {
                return new TrackdayProperties
                {
                    AiLevel = (base.RaceGridViewModel.get_AiLevelFixed() ? base.RaceGridViewModel.get_AiLevel() : 100.0),
                    Penalties = base.Penalties,
                    JumpStartPenalty = (JumpStartPenaltyType)0,
                    StartingPosition = 1,
                    RaceLaps = base.LapsNumber,
                    BotCars = botCars,
                    // Force native Track Day so CSP AI Flood can initialize.
                    UsePracticeSessionType = false,
                    SpeedLimit = SpeedLimit,
                    // Added:
                    Duration = (double)TrackdayDuration
                };
            }
        }

        // Added:
        private void EnsureDurationSlider()
        {
            // Injected WPF UI code creating StackPanel with ValueLabel and Slider
            // Placed at Column 1, Row 2 (directly mirroring Weekend mode's Practice slider)
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_loaded)
            {
                _loaded = true;
                // Added call to inject slider into visual tree:
                this.EnsureDurationSlider();
                ActualModel.Load();
            }
        }
    }
}
