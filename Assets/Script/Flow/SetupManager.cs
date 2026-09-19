using System;
using System.Collections.Generic;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Flow
{
    public class SetupManager : Singleton<SetupManager>, ISetupReporter
    {
        private ISetupInitializer[] initializers = new ISetupInitializer[0];
        private ISetupRunner[] runners = new ISetupRunner[0];

        private readonly HashSet<ISetupInitializer> readySet = new HashSet<ISetupInitializer>();

        private bool setupStarted;
        private bool setupCompleted;

        public event Action<float> OnSetupProgress;
        public event Action OnSetupCompleted;

        public bool IsSetupStarted
        {
            get { return setupStarted; }
        }

        public bool IsCompleted
        {
            get { return setupCompleted; }
        }

        public int TotalCount
        {
            get { return initializers.Length; }
        }

        public int CompletedCount
        {
            get { return readySet.Count; }
        }

        public float Progress01
        {
            get
            {
                if (!setupStarted)
                {
                    return 0f;
                }

                if (setupCompleted)
                {
                    return 1f;
                }

                if (initializers.Length == 0)
                {
                    return 1f;
                }

                return Mathf.Clamp01((float)readySet.Count / initializers.Length);
            }
        }

        private void Start()
        {
            BeginSetup();
        }

        public void BeginSetup()
        {
            if (setupStarted)
            {
                return;
            }

            setupStarted = true;
            initializers = SetupRegistry.ConsumeInitializers();
            runners = SetupRegistry.ConsumeRunners();

            NotifyProgress();

            if (initializers.Length == 0)
            {
                CompleteSetup();
                return;
            }

            for (int i = 0; i < initializers.Length; i++)
            {
                initializers[i].Setup(this);
            }

            if (AreAllReady())
            {
                CompleteSetup();
            }
        }

        public void ReportReady(ISetupInitializer initializer)
        {
            if (setupCompleted) return;
            if (initializer == null) return;
            if (Array.IndexOf(initializers, initializer) < 0) return;
            if (!readySet.Add(initializer)) return;

            NotifyProgress();

            if (AreAllReady())
            {
                CompleteSetup();
            }
        }

        private bool AreAllReady()
        {
            for (int i = 0; i < initializers.Length; i++)
            {
                if (!readySet.Contains(initializers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void CompleteSetup()
        {
            if (setupCompleted) return;

            setupCompleted = true;
            NotifyProgress();

            if (OnSetupCompleted != null)
            {
                OnSetupCompleted();
            }

            for (int i = 0; i < runners.Length; i++)
            {
                runners[i].RunAfterSetup();
            }
        }

        private void NotifyProgress()
        {
            if (OnSetupProgress != null)
            {
                OnSetupProgress(Progress01);
            }
        }
    }
}
