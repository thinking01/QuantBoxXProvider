﻿using System;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace QuantBox
{
    public partial class XProvider
    {
        private class TimedTask
        {
            private readonly XProvider _provider;
            private readonly Timer _timer;
            private DateTime _lastTime;
            private int _validQutryCount;
            private int _inTimer;

            private bool InTradingSession()
            {
                var time = DateTime.Now.TimeOfDay;
                foreach (var range in _provider.SessionTimes) {
                    if (time >= range.Begin && time <= range.End) {
                        return true;
                    }
                }
                return false;
            }

            public TimedTask(XProvider provider)
            {
                _provider = provider;
                _timer = new Timer(500);
                _timer.Elapsed += TimerOnElapsed;
            }

            private void TimerOnElapsed(object sender, ElapsedEventArgs e)
            {
                if (Interlocked.Exchange(ref _inTimer, 1) != 0) {
                    return;
                }
                try {
                    DataQuery();
                    AutoConnect();
                }
                finally {
                    Interlocked.Exchange(ref _inTimer, 0);
                }
            }

            private void AutoConnect()
            {
                if (_provider.SessionTimes.Count == 0) {
                    return;
                }

                if (InTradingSession()) {
                    if (!_provider.IsConnected) {
                        _provider._connectManager.Post(new OnAutoReconnect());
                    }
                }
                else {
                    if (_provider.IsConnected) {
                        _provider._connectManager.Post(new OnAutoDisconnect());
                    }
                }
            }

            private void DataQuery()
            {
                if (!_provider.IsConnected ||
                    _provider.IsInstrumentProvider && !_provider._qryInstrumentCompleted) {
                    return;
                }

                if (_validQutryCount == 0) {
                    if ((DateTime.Now - _lastTime).TotalSeconds > _provider.TradingDataQueryInterval) {
                        _validQutryCount = 2;
                        _lastTime = DateTime.Now;
                    }
                }

                if (_validQutryCount < 1) {
                    return;
                }

                if (EnableQueryPosition) {
                    _provider._trader.QueryPositions();
                    _validQutryCount -= 1;
                    EnableQueryPosition = false;
                }
                if (EnableQueryAccount) {
                    _provider._trader.QueryAccount();
                    _validQutryCount -= 1;
                    EnableQueryAccount = false;
                }
            }

            public bool EnableQueryAccount { get; set; }
            public bool EnableQueryPosition { get; set; }

            public void Start()
            {
                if (!_timer.Enabled) {
                    _lastTime = DateTime.Now;
                    _validQutryCount = 2;
                    if (_provider.IsExecutionProvider) {
                        EnableQueryAccount = true;
                        EnableQueryPosition = false;
                    }
                    else {
                        EnableQueryAccount = false;
                        EnableQueryPosition = false;
                    }
                    _timer.Start();
                }
            }

            public void Stop()
            {
                _timer.Stop();
            }
        }
    }
}
