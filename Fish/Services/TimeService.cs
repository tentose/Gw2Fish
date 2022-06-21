using MudBlazor;

namespace Fish.Services
{
    public enum RegionType
    {
        Cantha,
        Tyria
    }

    public enum TimeStage
    {
        Night,
        Dawn,
        Day,
        Dusk,
    }

    public class TimeState
    {
        public RegionType Region;
        public int SecondsRemainingInStage;
        public TimeStage Stage;
    }

    public interface ITimeService
    {
        public TimeState GetTimeForRegion(RegionType region);
        public event EventHandler TimeChanged;
        public event EventHandler<TimeState> StageChanged;
        public event EventHandler<TimeState> StageAboutToChange;
        public TimeStage GetNextStage(TimeStage stage);
        public string GetIconForStage(TimeStage stage);
    }

    public class TimeService : ITimeService
    {
        private const int STAGE_ABOUT_TO_CHANGE_DIFFERENCE = 60;

#if DEBUG
        private const int DEBUG_OFFSET = 19 * 60;
#else
		private const int DEBUG_OFFSET = 0;
#endif

        private struct StageCutoff
        {
            public int Time;
            public TimeStage Stage;
        }

        private static readonly Dictionary<RegionType, StageCutoff[]> CUTOFFS = new Dictionary<RegionType, StageCutoff[]>
        {
            { RegionType.Cantha, new StageCutoff[]
                {
                    new StageCutoff {Time = 35 * 60, Stage = TimeStage.Night },
                    new StageCutoff {Time = 40 * 60, Stage = TimeStage.Dawn},
                    new StageCutoff {Time = 95 * 60, Stage = TimeStage.Day},
                    new StageCutoff {Time = 100 * 60, Stage = TimeStage.Dusk},
                    new StageCutoff {Time = (120 + 35) * 60, Stage = TimeStage.Night },
                }
            },
            { RegionType.Tyria, new StageCutoff[]
                {
                    new StageCutoff {Time = 25 * 60, Stage = TimeStage.Night },
                    new StageCutoff {Time = 30 * 60, Stage = TimeStage.Dawn },
                    new StageCutoff {Time = 100 * 60, Stage = TimeStage.Day },
                    new StageCutoff {Time = 105 * 60, Stage = TimeStage.Dusk },
                    new StageCutoff {Time = (120 + 25) * 60, Stage = TimeStage.Night },
                }
            }
        };

        private Dictionary<RegionType, TimeState> timeStates = new Dictionary<RegionType, TimeState>();

        private static readonly Dictionary<TimeStage, string> TIME_ICONS = new Dictionary<TimeStage, string>
        {
            { TimeStage.Night, Icons.Filled.NightsStay },
            { TimeStage.Dawn, Icons.Filled.WbTwilight },
            { TimeStage.Day, Icons.Filled.WbSunny },
            { TimeStage.Dusk, Icons.Filled.WbTwilight },
        };

        private Timer tickTimer;
        private int circularTime;
        private bool firstTick = true;

        public TimeService()
        {
            foreach (var cutoff in CUTOFFS)
            {
                var state = new TimeState();
                state.Region = cutoff.Key;
                timeStates.Add(cutoff.Key, state);
            }

            tickTimer = new Timer(cb => TickTimer(), null, 0, 1000);
        }

        private async Task TickTimer()
        {
            var now = System.DateTime.UtcNow;
            var hour = now.Hour % 2;
            circularTime = ((int)now.TimeOfDay.TotalSeconds + DEBUG_OFFSET) % (2 * 60 * 60);

            foreach (var state in timeStates)
            {
                UpdateTimeStateForRegion(state.Key, !firstTick);
            }
            OnTimeChanged();

            firstTick = false;
        }

        private void UpdateTimeStateForRegion(RegionType region, bool fireEvents = true)
        {
            bool stageChanged = false;
            bool stageAboutToChange = false;

            var cutoffs = CUTOFFS[region];
            for (int i = 0; i < cutoffs.Length; i++)
            {
                if (circularTime < cutoffs[i].Time)
                {
                    if (timeStates[region].Stage != cutoffs[i].Stage)
                    {
                        timeStates[region].Stage = cutoffs[i].Stage;
                        stageChanged = true;
                    }

                    var remainingTime = cutoffs[i].Time - circularTime;
                    timeStates[region].SecondsRemainingInStage = remainingTime;

                    if (remainingTime == STAGE_ABOUT_TO_CHANGE_DIFFERENCE)
                    {
                        stageAboutToChange = true;
                    }

                    break;
                }
            }

            if (fireEvents)
            {
                if (stageChanged)
                {
                    OnStageChanged(timeStates[region]);
                }
                if (stageAboutToChange)
                {
                    OnStageAboutToChange(timeStates[region]);
                }
            }
        }

        public TimeState GetTimeForRegion(RegionType region)
        {
            return timeStates[region];
        }

        public event EventHandler TimeChanged;
        private void OnTimeChanged() => TimeChanged?.Invoke(this, EventArgs.Empty);

        public event EventHandler<TimeState> StageChanged;
        private void OnStageChanged(TimeState state) => StageChanged?.Invoke(this, state);

        public event EventHandler<TimeState> StageAboutToChange;
        private void OnStageAboutToChange(TimeState state) => StageAboutToChange?.Invoke(this, state);

        public TimeStage GetNextStage(TimeStage stage)
        {
            if (stage == TimeStage.Night)
            {
                return TimeStage.Dawn;
            }
            else if (stage == TimeStage.Dawn)
            {
                return TimeStage.Day;
            }
            else if (stage == TimeStage.Day)
            {
                return TimeStage.Dusk;
            }
            else
            {
                return TimeStage.Night;
            }
        }

        public string GetIconForStage(TimeStage stage)
        {
            return TIME_ICONS[stage];
        }
    }
}
