using System;
using BroadcastMicGuard;

static class PolicyTests {
    static int count;
    static Snapshot Sample(float level=0.72f,bool muted=false,string endpoint="broadcast",uint state=1,double time=10) {
        return new Snapshot {Level=level,Muted=muted,Endpoint=endpoint,State=state,Time=time};
    }
    static Recovery Ready(Snapshot before=null,bool broadcast=true) {
        var r=new Recovery(); r.Observe(before ?? Sample(),broadcast); r.Suspend(11); r.Resume(100); return r;
    }
    static void Check(string name,bool actual,bool expected) {
        if(actual != expected) throw new Exception("FAIL "+name);
        Console.WriteLine("PASS "+name); count++;
    }
    static bool Decide(Recovery r,Snapshot current=null,double time=106) {
        float target; return r.Decide(current ?? Sample(0),time,out target);
    }
    static int Main() {
        try {
            float target; var r=Ready();
            Check("resume restores saved fractional level",r.Decide(Sample(0),106,out target) && Math.Abs(target-0.72f)<0.0001,true);
            Check("at most one restoration",Decide(r),false);
            Check("no pre-sleep baseline",Decide(new Recovery()),false);
            Check("baseline already zero",Decide(Ready(Sample(0))),false);
            Check("pre-sleep mute",Decide(Ready(Sample(0.72f,true))),false);
            Check("current mute",Decide(Ready(),Sample(0,true)),false);
            Check("Broadcast absent",Decide(Ready(null,false)),false);
            Check("stale baseline",Decide(Ready(Sample(time:1))),false);
            Check("inactive baseline",Decide(Ready(Sample(state:4))),false);
            Check("inactive current endpoint",Decide(Ready(),Sample(0,state:4)),false);
            Check("endpoint identity changed",Decide(Ready(),Sample(0,endpoint:"another")),false);
            Check("user positive level respected",Decide(Ready(),Sample(0.4f)),false);
            Check("too early",Decide(Ready(),time:104),false);
            Check("expired window",Decide(Ready(),time:131),false);
            Check("NaN rejected",Decide(Ready(),Sample(float.NaN)),false);
            r=Ready(); r.Notification(true,0); r.Notification(false,0);
            Check("mute then unmute cancels cycle",Decide(r),false);
            r=Ready(); r.Notification(false,0.3f); r.Notification(false,0);
            Check("positive change then zero cancels cycle",Decide(r),false);
            r=Ready(); r.Cancel("notification gap"); Check("lost notifications fail closed",Decide(r),false);
            r=Ready(); r.Cancel("device error"); Check("device error fails closed",Decide(r),false);
            r=Ready(); r.Resume(103); Check("duplicate resume does not delay decision",Decide(r,time:105),true);
            r=new Recovery(); r.Observe(Sample(),true); r.Resume(100);
            Check("resume without suspend cannot restore",Decide(r),false);
            r=Ready(); Check("early check does not consume attempt",Decide(r,time:102),false);
            Check("later eligible decision still works",Decide(r,time:106),true);
            r=Ready(); Decide(r,Sample(0.4f)); Check("ineligible decision is final",Decide(r),false);
            r=Ready(); Decide(r); r.Notification(true,0); Check("late mute cancels selected write",r.Reason=="restore pre-sleep level",false);
            r=Ready(); Decide(r); r.Notification(false,0.2f); Check("late positive notification cancels selected write",r.Reason=="restore pre-sleep level",false);
            r=Ready(); Decide(r); r.Observe(Sample(0.61f,time:200),true); r.Suspend(201); r.Resume(300);
            Check("second sleep cycle gets fresh baseline",r.Decide(Sample(0),306,out target) && Math.Abs(target-0.61f)<0.0001,true);
            Console.WriteLine(count+" tests passed"); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
