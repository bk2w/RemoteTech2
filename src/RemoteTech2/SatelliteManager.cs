﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace RemoteTech
{
    public class SatelliteManager : IEnumerable<VesselSatellite>, IDisposable
    {
        public event Action<VesselSatellite> OnRegister = delegate { };
        public event Action<VesselSatellite> OnUnregister = delegate { };
        public int Count { get { return satelliteCache.Count; } }
        public VesselSatellite this[Guid g] { get { return For(g); } }
        public VesselSatellite this[Vessel v] { get { if (v == null) return null; return For(v.id); } }

        private readonly Dictionary<Guid, List<ISignalProcessor>> loadedSpuCache =
            new Dictionary<Guid, List<ISignalProcessor>>();
        private readonly Dictionary<Guid, VesselSatellite> satelliteCache =
            new Dictionary<Guid, VesselSatellite>();

        public SatelliteManager()
        {
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);

            OnRegister += vs => RTLog.Notify("SatelliteManager: OnRegister({0})", vs);
            OnUnregister += vs => RTLog.Notify("SatelliteManager: OnUnregister({0})", vs);
        }

        /// <summary>
        /// Registers a signal processor for the vessel.
        /// </summary>
        /// <param name="vessel">The vessel.</param>
        /// <param name="spu">The signal processor.</param>
        /// <returns>Guid key under which the signal processor was registered.</returns>
        public Guid Register(ISignalProcessor spu)
        {
            RTLog.Notify("SatelliteManager: Register({0})", spu);
            Guid key = spu.Guid;

            if (!loadedSpuCache.ContainsKey(key))
            {
                UnregisterProto(spu.Vessel);
                loadedSpuCache[key] = new List<ISignalProcessor>();
            }

            // Add if non duplicate
            if (!loadedSpuCache[key].Contains(spu))
            {
                // Create a new VesselSatellite if necessary.
                if (loadedSpuCache[key].Count == 0)
                {
                    satelliteCache[key] = new VesselSatellite(key, loadedSpuCache[key]);
                    OnRegister.Invoke(satelliteCache[key]);
                }

                loadedSpuCache[key].Add(spu);
                spu.Destroyed += Unregister;
            }

            return key;
        }

        /// <summary>
        /// Unregisters the specified signal processor.
        /// </summary>
        /// <param name="spu">The signal processor.</param>
        public void Unregister(ISignalProcessor spu)
        {
            Guid key = spu.Guid;
            Unregister(key, spu);
        }

        private void Unregister(Guid key, ISignalProcessor spu)
        {
            RTLog.Notify("SatelliteManager: Unregister({0}, {1})", key, spu);

            // Return if nothing to unregister.
            if (!loadedSpuCache.ContainsKey(key)) return;

            // Unregister the SignalProcessor. Unregister the VesselSatellite and fire appropriate events if it has no more SignalProcessors attached.
            // Register a ProtoSignalProcessor to be sure the Vessel didn't just go out of range. OnVesselDestroy will clean it up anyway.
            if (loadedSpuCache[key].Contains(spu))
            {
                loadedSpuCache[key].Remove(spu);
                if (loadedSpuCache[key].Count == 0)
                {
                    OnUnregister(satelliteCache[key]);
                    satelliteCache.Remove(key);
                    loadedSpuCache.Remove(key);
                    spu.Destroyed -= Unregister;
                    RegisterProto(spu.Vessel);
                }
            }
        }

        /// <summary>
        /// Registers a ProtoSignalProcessor compiled from the unloaded vessel data.
        /// </summary>
        /// <param name="vessel">The vessel.</param>
        public void RegisterProto(Vessel vessel)
        {
            RTLog.Notify("SatelliteManager: RegisterProto({0})", vessel);

            Guid key = vessel.id;
            // Return if there are still real SignalProcessors loaded.
            if (loadedSpuCache.ContainsKey(key)) return;

            // Retrieves a ProtoSignalProcessor from the vessel's data if it exists.
            ISignalProcessor spu = vessel.GetSignalProcessor();

            // Fire events for the currently registered VesselSatellite if it exists.
            UnregisterProto(vessel);

            // Register the ProtoSignalProcessor by attaching it to a new VesselSatellite.
            if (spu != null)
            {
                satelliteCache[key] = new VesselSatellite(key, new List<ISignalProcessor>(new [] { spu }));
                OnRegister(satelliteCache[key]);
            }
        }

        /// <summary>
        /// Unregisters the VesselSatellite that was seeded with a ProtoSignalProcessor.
        /// </summary>
        /// <param name="vessel">The vessel.</param>
        public void UnregisterProto(Vessel vessel)
        {
            RTLog.Notify("SatelliteManager: UnregisterProto({0})", vessel);

            Guid key = vessel.id;
            // Return if there are still real SignalProcessors loaded.
            if (loadedSpuCache.ContainsKey(key)) return;

            // Unregister satellite if it exists.
            if (satelliteCache.ContainsKey(key))
            {
                OnUnregister(satelliteCache[key]);
                satelliteCache.Remove(key);
            }
        }

        /// <summary>
        /// Loops over all registered SignalProcessors (real or not), returning the ones that are valid command stations.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ISatellite> FindCommandStations()
        {
            return satelliteCache.Values.Where(vs => vs.IsCommandStation).Cast<ISatellite>();
        }

        /* External Call */
        public void OnFixedUpdate() {
            foreach (var pair in loadedSpuCache.ToList())
            {
                foreach (var spu in pair.Value.ToList())
                {
                    if (spu.Guid != pair.Key)
                    {
                        Unregister(pair.Key, spu);
                        Register(spu);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a VesselSatellite registered to the specific key. Usually accessed by vessel instead.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private VesselSatellite For(Guid key)
        {
            VesselSatellite result;
            if (satelliteCache.TryGetValue(key, out result)) return result;
            return null;
        }

        private void OnVesselCreate(Vessel vessel)
        {
            RTLog.Notify("SatelliteManager: OnVesselCreate({0})", vessel);
        }

        private void OnVesselDestroy(Vessel vessel)
        {
            RTLog.Notify("SatelliteManager: OnVesselDestroy({0})", vessel);
            UnregisterProto(vessel);
        }

        public void Dispose()
        {
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
        }

        public IEnumerator<VesselSatellite> GetEnumerator()
        {
            return satelliteCache.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}