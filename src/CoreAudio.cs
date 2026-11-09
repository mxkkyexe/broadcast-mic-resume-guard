using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BroadcastMicGuard {
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMEnumerator {}
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMEnumerator {
        void EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        void GetDefaultAudioEndpoint(int flow, int role, out IntPtr device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice {
        void Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        void OpenPropertyStore(uint access, out IntPtr store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out uint state);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IEndpointVolume {
        void RegisterControlChangeNotify(IVolumeCallback callback);
        void UnregisterControlChangeNotify(IVolumeCallback callback);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float level, ref Guid context);
        void SetMasterVolumeLevelScalar(float level, ref Guid context);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        void GetChannelVolumeLevel(uint channel, out float level);
        void GetChannelVolumeLevelScalar(uint channel, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IVolumeCallback { [PreserveSig] int OnNotify(IntPtr data); }
    [StructLayout(LayoutKind.Sequential)] public struct VolumeNotice {
        public Guid Context; public int Muted; public float Scalar; public uint Channels;
    }
    public sealed class Endpoint {
        public string Id, Name;
        public override string ToString() { return Name+" | "+Id; }
    }
    public sealed class CoreAudio : IDisposable {
        IMMEnumerator enumerator; IMMDevice device; IEndpointVolume volume;
        IVolumeCallback callback; readonly string id;
        public CoreAudio(string endpoint) {
            id=endpoint;
            try {
                enumerator=(IMMEnumerator)new MMEnumerator(); enumerator.GetDevice(id,out device);
                Guid iid=typeof(IEndpointVolume).GUID; object obj;
                device.Activate(ref iid,23,IntPtr.Zero,out obj); volume=(IEndpointVolume)obj;
            } catch { Dispose(); throw; }
        }
        public Snapshot Read(double now) {
            uint state; float level; bool muted;
            device.GetState(out state); volume.GetMasterVolumeLevelScalar(out level); volume.GetMute(out muted);
            return new Snapshot {Endpoint=id,Level=level,Muted=muted,State=state,Time=now};
        }
        public void Subscribe(IVolumeCallback sink) { volume.RegisterControlChangeNotify(sink); callback=sink; }
        public bool Restore(float target) {
            Snapshot current=Read(0);
            if(current.State != 1 || current.Muted || current.Level != 0 || target <= 0 || target > 1 || float.IsNaN(target)) return false;
            Guid context=new Guid("6B196727-B644-468F-B8B9-A806F57D7637");
            // No SetMute call anywhere in this program. Windows has no atomic volume/mute compare-and-set.
            volume.SetMasterVolumeLevelScalar(target,ref context); return true;
        }
        public void Dispose() {
            if(volume != null) {
                if(callback != null) { try { volume.UnregisterControlChangeNotify(callback); } catch(COMException) {} }
                Marshal.ReleaseComObject(volume); volume=null;
            }
            if(device != null) { Marshal.ReleaseComObject(device); device=null; }
            if(enumerator != null) { Marshal.ReleaseComObject(enumerator); enumerator=null; }
        }
        public static List<Endpoint> Discover() {
            var matches=new List<Endpoint>();
            using(var root=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture")) {
                if(root == null) return matches;
                foreach(string key in root.GetSubKeyNames()) {
                    using(var properties=root.OpenSubKey(key+@"\Properties")) {
                        if(properties == null) continue;
                        string name=Convert.ToString(properties.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2"));
                        string description=Convert.ToString(properties.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6"));
                        if(description.IndexOf("NVIDIA Broadcast",StringComparison.OrdinalIgnoreCase) < 0
                            && name.IndexOf("NVIDIA Broadcast",StringComparison.OrdinalIgnoreCase) < 0) continue;
                        matches.Add(new Endpoint {Id="{0.0.1.00000000}."+key,Name=name+" ("+description+")"});
                    }
                }
            }
            return matches;
        }
    }
}
