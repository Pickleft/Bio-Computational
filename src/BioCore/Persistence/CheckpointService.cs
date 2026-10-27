using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Persistence;

/// <summary>
/// Binary checkpoint of the full dynamic state. Topology is NOT stored: it is rebuilt
/// deterministically from the config seed on load, then every piece of dynamic state is
/// overwritten, so a resumed run is bit-identical to an uninterrupted one (tested).
/// </summary>
public static class CheckpointService
{
    private const uint Magic = 0x42494F43; // "BIOC"

    public static void Save(SpikingNetwork net, string path)
    {
        // the data directory may not exist yet (fresh clone, VS working dir, first run)
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        w.Write(Magic);
        w.Write(1); // version
        w.Write(net.TickIndex);
        w.Write(net.Config.Seed);
        w.Write(net.RewardPredictor);
        w.Write(net.SpikesThisTick);

        foreach (var s in net.Rng.GetState()) w.Write(s);

        Write(w, net.V); Write(w, net.U); Write(w, net.LastPostSpikeMs);
        Write(w, net.Cond.XAmpa); Write(w, net.Cond.XNmda); Write(w, net.Cond.XGabaa); Write(w, net.Cond.XGabab);
        Write(w, net.Cond.GAmpa); Write(w, net.Cond.GNmda); Write(w, net.Cond.GGabaa); Write(w, net.Cond.GGabab);
        Write(w, net.Syn.WeightQ15, asShort: true);
        Write(w, net.Syn.TmR); Write(w, net.Syn.TmU); Write(w, net.Syn.LastDeliveryMs);
        Write(w, net.Learning.Eligibility);

        foreach (var t in net.Modulators.Tone) w.Write(t);
        foreach (var p in net.Modulators.PendingRelease) w.Write(p);

        for (int r = 0; r < net.Transmitters.RegionCount; r++)
        {
            foreach (var v in net.Transmitters.Pools[r]) w.Write(v);
            foreach (var v in net.Transmitters.PendingRelease[r]) w.Write(v);
        }

        w.Write(net.Energy.Spent);
        w.Write(net.Energy.SpikeCount);

        w.Write(net.Recorder.TotalSpikes);
        w.Write(net.Recorder.EventHead);
        w.Write(net.Recorder.EventCount);
        foreach (var c in net.Recorder.Counts) w.Write(c);
        foreach (var (t, n) in net.Recorder.Events) { w.Write(t); w.Write(n); }

        foreach (var u in net.WindowCounts) w.Write(u);

        for (int i = 0; i < net.Syn.NeuronCount + 1; i++) w.Write(net.Syn.RowPtr[i]); // topology sanity
        for (int slot = 0; slot < SpikingNetwork.RingSize; slot++)
        {
            var list = net.RingAt(slot);
            w.Write(list.Count);
            foreach (var s in list) w.Write(s);
        }
        w.Flush();
    }

    public static SpikingNetwork Load(string path, BrainConfig config)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var r = new BinaryReader(fs);
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("not a brain checkpoint");
        if (r.ReadInt32() != 1) throw new InvalidDataException("unsupported checkpoint version");
        long savedTick = r.ReadInt64();
        if (r.ReadUInt64() != config.Seed) throw new InvalidDataException("checkpoint seed does not match config");

        var net = new SpikingNetwork(config);
        net.TickIndex = savedTick;
        net.RewardPredictor = r.ReadSingle();
        net.SpikesThisTick = r.ReadInt32();
        net.Rng.SetState([r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64()]);

        Read(r, net.V); Read(r, net.U); Read(r, net.LastPostSpikeMs);
        Read(r, net.Cond.XAmpa); Read(r, net.Cond.XNmda); Read(r, net.Cond.XGabaa); Read(r, net.Cond.XGabab);
        Read(r, net.Cond.GAmpa); Read(r, net.Cond.GNmda); Read(r, net.Cond.GGabaa); Read(r, net.Cond.GGabab);
        ReadShort(r, net.Syn.WeightQ15);
        Read(r, net.Syn.TmR); Read(r, net.Syn.TmU); Read(r, net.Syn.LastDeliveryMs);
        Read(r, net.Learning.Eligibility);

        for (int k = 0; k < net.Modulators.Tone.Length; k++) net.Modulators.Tone[k] = r.ReadSingle();
        for (int k = 0; k < net.Modulators.PendingRelease.Length; k++) net.Modulators.PendingRelease[k] = r.ReadSingle();

        for (int reg = 0; reg < net.Transmitters.RegionCount; reg++)
        {
            for (int k = 0; k < TransmitterSystem.KindCount; k++) net.Transmitters.Pools[reg][k] = r.ReadSingle();
            for (int k = 0; k < TransmitterSystem.KindCount; k++) net.Transmitters.PendingRelease[reg][k] = r.ReadSingle();
        }

        net.Energy.Restore(r.ReadSingle(), r.ReadInt64());

        net.Recorder.Restore(r.ReadInt64(), r.ReadInt32(), r.ReadInt32());
        for (int i = 0; i < net.Recorder.Counts.Length; i++) net.Recorder.Counts[i] = r.ReadUInt32();
        for (int i = 0; i < net.Recorder.Events.Length; i++)
        {
            float t = r.ReadSingle(); int n = r.ReadInt32();
            net.Recorder.Events[i] = (t, n);
        }
        for (int i = 0; i < net.WindowCounts.Length; i++) net.WindowCounts[i] = r.ReadUInt32();

        for (int i = 0; i < net.Syn.NeuronCount + 1; i++)
            if (r.ReadInt32() != net.Syn.RowPtr[i]) throw new InvalidDataException("topology mismatch on load");

        for (int slot = 0; slot < SpikingNetwork.RingSize; slot++)
        {
            int count = r.ReadInt32();
            var list = net.RingAt(slot);
            list.Clear();
            for (int i = 0; i < count; i++) list.Add(r.ReadInt32());
        }
        return net;
    }

    private static void Write(BinaryWriter w, float[] a)
    {
        w.Write(a.Length);
        foreach (var v in a) w.Write(v);
    }

    private static void Write(BinaryWriter w, short[] a, bool asShort)
    {
        w.Write(a.Length);
        foreach (var v in a) w.Write(v);
    }

    private static void Read(BinaryReader r, float[] a)
    {
        int len = r.ReadInt32();
        if (len != a.Length) throw new InvalidDataException("array length mismatch");
        for (int i = 0; i < a.Length; i++) a[i] = r.ReadSingle();
    }

    private static void ReadShort(BinaryReader r, short[] a)
    {
        int len = r.ReadInt32();
        if (len != a.Length) throw new InvalidDataException("array length mismatch");
        for (int i = 0; i < a.Length; i++) a[i] = r.ReadInt16();
    }
}
