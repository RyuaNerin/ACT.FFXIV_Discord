using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACT.FFXIV_Discord
{
    internal class FFXIVPluginWrapper : IDisposable
    {
        private readonly object plugin;

        public FFXIVPluginWrapper(object plugin)
        {
            this.plugin = plugin;
        }
        ~FFXIVPluginWrapper()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool disposed;
        public void Dispose(bool disposing)
        {
            if (this.disposed) return;
            this.disposed = true;

            if (disposing)
            {
                this.DataSubscription?.Dispose();
            }
        }

        public object Raw => this.plugin;

        private IDataSubscription dataSubscription;
        public IDataSubscription DataSubscription
            => this.dataSubscription ?? (this.dataSubscription = new IDataSubscription(plugin.GetType().GetProperty("DataSubscription").GetValue(plugin)));

        private IDataRepository dataRepository;
        public IDataRepository DataRepository
            => this.dataRepository ?? (this.dataRepository = new IDataRepository(plugin.GetType().GetProperty("DataRepository").GetValue(plugin)));

        public class IDataSubscription : IDisposable
        {
            private readonly object dataSubscription;

            public IDataSubscription(object dataSubscription)
            {
                this.dataSubscription = dataSubscription;
            }
            ~IDataSubscription()
            {
                this.Dispose(false);
            }

            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            private bool disposed;
            public void Dispose(bool disposing)
            {
                if (this.disposed) return;
                this.disposed = true;

                if (disposing)
                {
                    this.UnregisterEvents();
                }
            }

            public delegate void PrimaryPlayerDelegate();
            public delegate void ZoneChangedDelegate(uint ZoneID, string ZoneName);
            public delegate void PlayerStatsChangedDelegate(IPlayer playerStats);

            public event PrimaryPlayerDelegate PrimaryPlayerChanged;
            public event ZoneChangedDelegate ZoneChanged;
            public event PlayerStatsChangedDelegate PlayerStatsChanged;

            private bool registeredEvents;
            public void RegisterEvents()
            {
                if (this.registeredEvents) return;
                this.registeredEvents = true;

                this.AddEvent("PrimaryPlayerChanged");
                this.AddEvent("ZoneChanged"         );
                this.AddEvent("PlayerStatsChanged"  );
            }
            private void UnregisterEvents()
            {
                if (!this.registeredEvents) return;

                this.RemoveEvent("PrimaryPlayerChanged");
                this.RemoveEvent("ZoneChanged"         );
                this.RemoveEvent("PlayerStatsChanged"  );
            }

            private void PrimaryPlayerChangedInner()
            {
                this.PrimaryPlayerChanged?.Invoke();
            }
            private void ZoneChangedInner(uint ZoneID, string ZoneName)
            {
                this.ZoneChanged?.Invoke(ZoneID, ZoneName);
            }
            private void PlayerStatsChangedInner(object playerStats)
            {
                this.PlayerStatsChanged?.Invoke(new IPlayer(playerStats));
            }


            private void AddEvent(string eventName)
            {
                var @event = this.dataSubscription.GetType().GetEvent(eventName);
                var eventType = @event.EventHandlerType;
                var method = this.GetType().GetMethod(eventName + "Inner", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate(eventType, this);

                @event.AddEventHandler(this.dataSubscription, method);
            }

            private void RemoveEvent(string eventName)
            {

                var @event = this.dataSubscription.GetType().GetEvent(eventName);
                var eventType = @event.EventHandlerType;
                var method = this.GetType().GetMethod(eventName + "Inner", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate(eventType, this);

                @event.RemoveEventHandler(this.dataSubscription, method);
            }
        }

        public class IDataRepository
        {
            private readonly object dataRepository;

            public IDataRepository(object dataRepository)
            {
                this.dataRepository = dataRepository;
            }

            public uint GetCurrentPlayerID()
            {
                return (uint)this.dataRepository.GetType().GetMethod("GetCurrentPlayerID").Invoke(this.dataRepository, null);
            }

            public IPlayer GetPlayer()
            {
                return new IPlayer(this.dataRepository.GetType().GetMethod("GetPlayer").Invoke(this.dataRepository, null));
            }

            public IEnumerable<ICombatant> GetCombatantList()
            {
                var combatantList = this.dataRepository.GetType().GetMethod("GetCombatantList").Invoke(this.dataRepository, null) as IEnumerable;

                return combatantList.OfType<object>().Select(e => new ICombatant(e));
            }
        }

        public class IPlayer
        {
            private readonly object player;
            public IPlayer(object player)
            {
                this.player = player;
            }

            public uint JobID => (uint)this.player.GetType().GetProperty("JobID").GetValue(this.player);
        }

        public class ICombatant
        {
            private readonly object combatant;
            public ICombatant(object combatant)
            {
                this.combatant = combatant;
            }

            public uint   ID   => (uint  )this.combatant.GetType().GetProperty("ID"  ).GetValue(this.combatant);
            public string Name => (string)this.combatant.GetType().GetProperty("Name").GetValue(this.combatant);
        }
    }
}
