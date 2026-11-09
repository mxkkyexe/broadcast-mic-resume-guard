using System;

namespace BroadcastMicGuard {
    public sealed class Snapshot {
        public string Endpoint;
        public float Level;
        public bool Muted;
        public uint State;
        public double Time;
    }

    // No Windows dependencies: all decisions can be tested without touching a microphone.
    public sealed class Recovery {
        Snapshot last, before;
        bool broadcast, suspended, cancelled = true, attempted = true;
        double resumed = -1;
        public string Reason { get; private set; }

        public void Observe(Snapshot sample, bool broadcastRunning) {
            if(sample.Muted) Cancel("mute observed");
            if(resumed >= 0 && !attempted && last != null && sample.Level > 0 && sample.Level != last.Level)
                Cancel("positive volume change");
            last = sample; broadcast = broadcastRunning;
        }
        public void Suspend(double now) {
            before = last != null && now >= last.Time && now-last.Time <= 3 ? last : null;
            suspended = true; resumed = -1; attempted = false;
            cancelled = before == null || before.Muted || before.Level <= 0 || before.State != 1 || !broadcast;
            Reason = cancelled ? "no safe pre-sleep baseline" : "armed";
        }
        public void Resume(double now) {
            // Windows sends automatic resume, then sometimes user resume. Do not re-arm twice.
            if(!suspended) return;
            suspended = false; resumed = now;
        }
        public void Notification(bool muted, float level) {
            if(muted) Cancel("mute notification");
            if(resumed >= 0 && level > 0) Cancel("positive volume notification");
        }
        public void Cancel(string reason) { cancelled = true; Reason = reason; }
        public bool Decide(Snapshot current, double now, out float target) {
            target = 0;
            if(attempted || resumed < 0 || now-resumed < 5) return false;
            attempted = true; // Exactly one decision per observed sleep cycle.
            if(cancelled) return false;
            if(now-resumed > 30) { Reason="resume window expired"; return false; }
            if(before == null || current == null || current.Endpoint != before.Endpoint || current.State != 1
                || current.Muted || current.Level > 0 || float.IsNaN(current.Level)
                || before.Level <= 0 || before.Level > 1 || float.IsNaN(before.Level)) {
                Reason="endpoint or level no longer eligible"; return false;
            }
            target=before.Level; Reason="restore pre-sleep level"; return true;
        }
    }
}
