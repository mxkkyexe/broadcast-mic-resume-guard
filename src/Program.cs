using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace BroadcastMicGuard {
    internal static class Program {
        internal static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BroadcastMicResumeGuard");
        [STAThread] static int Main(string[] args) {
            Directory.CreateDirectory(Data);
            try {
                if(args.Length > 0 && args[0] == "--stop") {
                    try { using(var stop=EventWaitHandle.OpenExisting(@"Local\BroadcastMicResumeGuard.Stop")) stop.Set(); }
                    catch(WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                if(args.Length > 0 && args[0] == "--diagnose") {
                    string path=args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Data,"diagnostics.txt");
                    var lines=new System.Collections.Generic.List<string>();
                    lines.Add("Broadcast Mic Resume Guard 0.1.0 | "+DateTime.UtcNow.ToString("o"));
                    foreach(var endpoint in CoreAudio.Discover()) {
                        try { using(var audio=new CoreAudio(endpoint.Id)) {
                            var s=audio.Read(0); lines.Add(endpoint+" | level="+s.Level+" muted="+s.Muted+" state="+s.State);
                        }} catch(Exception ex) { lines.Add(endpoint+" | "+ex.Message); }
                    }
                    if(lines.Count == 1) lines.Add("No Broadcast capture endpoints found.");
                    File.WriteAllLines(path,lines); return 0;
                }
                bool owned;
                using(var mutex=new Mutex(true,@"Local\BroadcastMicResumeGuard.Portable",out owned)) {
                    if(!owned) return 0;
                    try { Application.EnableVisualStyles(); Application.Run(new TrayContext(Array.IndexOf(args,"--observe") >= 0)); }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            } catch(Exception ex) {
                File.AppendAllText(Path.Combine(Data,"startup-error.log"),DateTime.UtcNow.ToString("o")+" "+ex+Environment.NewLine);
                MessageBox.Show(ex.Message,"Broadcast Mic Resume Guard",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    internal sealed class Log {
        readonly string path=Path.Combine(Program.Data,"events.log");
        public void Write(string value) {
            // Called only on the message-loop thread. Keep one backup, at most ~2 MB total.
            if(File.Exists(path) && new FileInfo(path).Length > 1024*1024) {
                if(File.Exists(path+".1")) File.Delete(path+".1");
                File.Move(path,path+".1");
            }
            File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+value+Environment.NewLine);
        }
    }
    internal sealed class PowerWindow : NativeWindow, IDisposable {
        readonly Action<bool> power;
        public PowerWindow(Action<bool> onPower) {
            power=onPower;
            // A hidden top-level window, not a message-only window: receives power broadcasts.
            CreateHandle(new CreateParams {Caption="BroadcastMicResumeGuard.Power",Style=0,ExStyle=0x80});
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg == 0x218) {
                int kind=message.WParam.ToInt32();
                if(kind == 4 || kind == 18 || kind == 7) { power(kind == 4); message.Result=new IntPtr(1); return; }
            }
            base.WndProc(ref message);
        }
        public void Dispose() { DestroyHandle(); }
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class NoticeSink : IVolumeCallback {
        readonly ConcurrentQueue<VolumeNotice> queue=new ConcurrentQueue<VolumeNotice>();
        int lost;
        public int OnNotify(IntPtr data) {
            // Windows requires nonblocking callbacks: no file I/O, COM calls or locks here.
            try {
                if(queue.Count >= 256) Interlocked.Exchange(ref lost,1);
                else queue.Enqueue((VolumeNotice)Marshal.PtrToStructure(data,typeof(VolumeNotice)));
            } catch { Interlocked.Exchange(ref lost,1); }
            return 0;
        }
        public bool Take(out VolumeNotice notice) { return queue.TryDequeue(out notice); }
        public bool Lost() { return Interlocked.Exchange(ref lost,0) != 0; }
    }
    internal sealed class TrayContext : ApplicationContext {
        readonly Recovery recovery=new Recovery(); readonly Log log=new Log();
        readonly NoticeSink sink=new NoticeSink(); readonly PowerWindow window;
        readonly NotifyIcon tray; readonly System.Windows.Forms.Timer timer;
        readonly EventWaitHandle stop=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\BroadcastMicResumeGuard.Stop");
        readonly bool observeOnly;
        CoreAudio audio; string endpoint, lastSample, lastError;
        double nextDiscovery, nextProcesses; bool broadcast; int ticks;
        static double Now { get { return (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency; } }
        public TrayContext(bool observe) {
            observeOnly=observe;
            var menu=new ContextMenuStrip();
            menu.Items.Add("Open diagnostic logs",null,delegate { Process.Start("explorer.exe",Program.Data); });
            menu.Items.Add("Exit guard",null,delegate { ExitThread(); });
            tray=new NotifyIcon {Icon=SystemIcons.Information,Text="Broadcast mic guard: starting",ContextMenuStrip=menu,Visible=true};
            window=new PowerWindow(OnPower);
            timer=new System.Windows.Forms.Timer {Interval=500}; timer.Tick+=Tick;
            log.Write("START version=0.1.0 observeOnly="+observeOnly); timer.Start(); Tick(this,EventArgs.Empty);
        }
        void Drain() {
            if(sink.Lost()) recovery.Cancel("notification gap");
            VolumeNotice n;
            while(sink.Take(out n)) {
                recovery.Notification(n.Muted != 0,n.Scalar);
                log.Write("NOTIFY level="+n.Scalar.ToString(CultureInfo.InvariantCulture)+" muted="+(n.Muted != 0)+" context="+n.Context);
            }
        }
        void OnPower(bool suspend) {
            try {
                Drain();
                if(suspend) recovery.Suspend(Now); else recovery.Resume(Now);
                log.Write(suspend ? "POWER suspend baseline="+recovery.Reason : "POWER resume");
            } catch(Exception) { recovery.Cancel("power handler failure"); }
        }
        static bool Running(string name) {
            var processes=Process.GetProcessesByName(name);
            bool found=processes.Length != 0;
            foreach(var p in processes) p.Dispose();
            return found;
        }
        void Discover(double now) {
            nextDiscovery=now+10;
            var candidates=CoreAudio.Discover();
            var active=new System.Collections.Generic.List<string>();
            foreach(var candidate in candidates) {
                try { using(var probe=new CoreAudio(candidate.Id)) { if(probe.Read(now).State == 1) active.Add(candidate.Id); }}
                catch(COMException) { }
            }
            if(active.Count != 1) {
                if(audio != null) { recovery.Cancel("endpoint unavailable or ambiguous"); audio.Dispose(); audio=null; }
                tray.Text=active.Count == 0 ? "Broadcast mic guard: waiting for endpoint" : "Broadcast mic guard: ambiguous endpoints";
                return;
            }
            if(audio == null || endpoint != active[0]) {
                recovery.Cancel("endpoint connection changed");
                if(audio != null) audio.Dispose();
                endpoint=active[0]; audio=new CoreAudio(endpoint); audio.Subscribe(sink);
                log.Write("CONNECTED endpoint="+endpoint); lastSample=null;
            }
        }
        void Tick(object sender, EventArgs args) {
            try {
                if(stop.WaitOne(0)) { ExitThread(); return; }
                double now=Now;
                if(now >= nextProcesses) {
                    bool previous=broadcast; broadcast=Running("NVIDIA Broadcast"); nextProcesses=now+2;
                    if(previous != broadcast) log.Write("BROADCAST running="+broadcast);
                }
                if(now >= nextDiscovery) Discover(now);
                Drain(); if(audio == null) return;
                Snapshot sample=audio.Read(now);
                recovery.Observe(sample,broadcast);
                string description="level="+sample.Level.ToString(CultureInfo.InvariantCulture)+" muted="+sample.Muted+" state="+sample.State;
                if(description != lastSample) { log.Write("SAMPLE "+description); lastSample=description; }
                float target;
                if(recovery.Decide(sample,now,out target)) {
                    Drain(); // Catch queued mute/volume changes immediately before considering a write.
                    // Drain can cancel recovery after Decide; explicitly honor that cancellation.
                    bool applied=!observeOnly && recovery.Reason == "restore pre-sleep level" && audio.Restore(target);
                    log.Write("RESTORE applied="+applied+" target="+target.ToString(CultureInfo.InvariantCulture));
                    Snapshot verified=audio.Read(Now);
                    log.Write("VERIFY level="+verified.Level+" muted="+verified.Muted);
                }
                if(++ticks % 20 == 0) log.Write("STATUS "+description+" recovery="+recovery.Reason);
                tray.Text=observeOnly ? "Broadcast mic guard: observation only" : "Broadcast mic guard: monitoring"; lastError=null;
            } catch(Exception ex) {
                recovery.Cancel("endpoint or monitoring error");
                tray.Text="Broadcast mic guard: error; see logs";
                if(lastError != ex.Message) { try { log.Write("ERROR "+ex.Message); } catch {} lastError=ex.Message; }
                if(audio != null) { audio.Dispose(); audio=null; }
                nextDiscovery=Now+5;
            }
        }
        protected override void ExitThreadCore() {
            timer.Stop(); timer.Dispose(); window.Dispose();
            stop.Dispose();
            if(audio != null) audio.Dispose();
            tray.Visible=false; tray.Dispose();
            try { log.Write("STOP"); } catch {}
            base.ExitThreadCore();
        }
    }
}
