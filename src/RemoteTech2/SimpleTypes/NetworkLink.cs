using System;
using System.Collections.Generic;

namespace RemoteTech
{
    public class NetworkLink<T> : IEquatable<NetworkLink<T>>
    {
        public readonly T Target;
        public readonly List<IAntenna> Transmitters;
        public readonly List<IAntenna> Receivers;
        public readonly LinkType Port;
        public double Cost { get; set;}

        public NetworkLink(T sat, List<IAntenna> tx_ant, List<IAntenna> rx_ant, LinkType port, double cost)
        {
            Target = sat;
            Transmitters = tx_ant;
            Receivers = rx_ant;
            Port = port;
            Cost = cost;
        }

        public bool Equals(NetworkLink<T> o)
        {
            if (o == null) return false;
            if (!Target.Equals(o.Target)) return false;
            return true;
        }

        public override string ToString()
        {
            return String.Format("NetworkLink(T: {0}, TX: {1}, RX: {2} P: {3}, C: {4})", Target, Transmitters.ToDebugString(), Receivers.ToDebugString(), Port, Cost);
        }
    }
}