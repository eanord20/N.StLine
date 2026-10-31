using System.Diagnostics;
using System.Text.Json;
using NStLine.Core.Models;
using NStLine.Cutting.Services;

static CuttingTask Make(double stock, double kerf, params (double length,int qty)[] parts) => new() { Material=new LinearMaterial {Id="m",Name="QA6",StandardLength=stock,CutWidth=kerf},BarLength=stock,CutWidth=kerf,Parts=parts.Select((p,i)=>new PartRequirement{Id=$"p{i}",MaterialId="m",Name=$"P{i}",Length=p.length,Quantity=p.qty}).ToArray() };
static CuttingOptions Opt()=>new(){TimeLimitSeconds=1,MaxPatternCount=10000,ToleranceMm=.01};
static object Snapshot(CuttingTask t,CuttingResult r)=>new {input=new{t.BarLength,t.CutWidth,parts=t.Parts.Select(p=>new{p.Id,p.Length,p.Quantity})},status=r.Status.ToString(),r.Method,r.BestFoundReason,r.TotalBarCount,r.TotalRemainder,r.TotalPartLength,r.TotalCutLoss,r.UtilizationPercent,r.GeneratedPatterns,r.ExploredNodes,r.ElapsedSeconds,bars=r.Bars.Select(b=>new{b.Index,b.Remainder,b.CutLoss,parts=b.Parts.Select(p=>new{p.Source.Id,p.Source.Length,p.CopyIndex})}),patterns=r.Patterns.Select(p=>new{p.Multiplicity,p.Remainder,parts=p.Parts.Select(x=>new{x.Source.Id,x.Source.Length})})};
static int Oracle(int cap,int kerf,(int len,int qty)[] ps){var pieces=ps.SelectMany(p=>Enumerable.Repeat(p.len+kerf,p.qty)).OrderDescending().ToArray();int best=pieces.Length;var loads=new List<int>();void Go(int i){if(i==pieces.Length){best=Math.Min(best,loads.Count);return;}var seen=new HashSet<int>();for(int b=0;b<loads.Count;b++){int old=loads[b];if(old+pieces[i]>cap||!seen.Add(old))continue;loads[b]+=pieces[i];Go(i+1);loads[b]=old;}if(loads.Count+1>=best)return;loads.Add(pieces[i]);Go(i+1);loads.RemoveAt(loads.Count-1);}Go(0);return best;}
var jsonOpt=new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals};
void Emit(string name,object data)=>Console.WriteLine(JsonSerializer.Serialize(new{name,data},jsonOpt));
// Evidence goes only to the harness working directory. Bound every child invocation.
using var evidence = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), args.FirstOrDefault()=="cancel" ? "cancel-evidence.txt" : "suite-evidence.txt")){AutoFlush=true};
Console.SetOut(evidence);
using var watchdog = new Timer(_=>Environment.Exit(124),null,TimeSpan.FromSeconds(45),Timeout.InfiniteTimeSpan);
var engine=new CuttingEngine();
if(args.FirstOrDefault()=="cancel"){
 var t=Make(1000,1,(600,30000));using var cts=new CancellationTokenSource();var sw=Stopwatch.StartNew();long cancelAt=-1;var timer=new Timer(_=>{cancelAt=sw.ElapsedMilliseconds;cts.Cancel();},null,20,Timeout.Infinite);try{var r=engine.Solve(t,Opt(),cancellationToken:cts.Token);Emit("cancelReturned",Snapshot(t,r));}catch(Exception e){Emit("cancel",new{type=e.GetType().Name,cancelAt,elapsedMs=sw.ElapsedMilliseconds,delayAfterCancelMs=sw.ElapsedMilliseconds-cancelAt,peakBytes=Process.GetCurrentProcess().PeakWorkingSet64});}timer.Dispose();return;
}
foreach(var (name,t) in new[]{("decimal-ffd-trap",Make(1,0,(.6,1),(.5,1),(.3,1),(.2,3))),("tiny",Make(1e-10,0,(6e-11,2))),("overflow-total",Make(1e308,0,(9e307,2))),("floor-int-overflow",Make(6000,0,(.000001,2))),("decimal-exact",Make(.3,0,(.1,3))),("null-part",new CuttingTask{Material=new LinearMaterial(),BarLength=100,CutWidth=0,Parts=new PartRequirement[]{null!}})}){
 try{Emit(name,Snapshot(t,engine.Solve(t,Opt())));}catch(Exception e){Emit(name,new{error=e.ToString()});}
}
var rnd=new Random(601030);int mismatches=0,physical=0,notOptimal=0;var watch=Stopwatch.StartNew();
for(int n=0;n<3000;n++){
 int cap=rnd.Next(8,51),kerf=rnd.Next(0,3);var ps=Enumerable.Range(0,3).Select(_=>(len:rnd.Next(1,cap-kerf),qty:rnd.Next(1,4))).ToArray();
 var t=Make(cap/10.0,kerf/10.0,ps.Select(p=>(p.len/10.0,p.qty)).ToArray());int expected=Oracle(cap,kerf,ps);var r=engine.Solve(t,Opt());
 if(r.Status!=CalculationStatus.Optimal)notOptimal++;
 if(r.TotalBarCount!=expected){mismatches++;if(mismatches<=12)Emit("oracle-mismatch",new{caseIndex=n,expected,actual=Snapshot(t,r)});}
 foreach(var b in r.Bars){var load=b.Parts.Sum(p=>(decimal)p.Source.Length)+b.Parts.Count*(decimal)t.CutWidth;if(load>(decimal)t.BarLength){physical++;if(physical<=5)Emit("physical-excess",new{caseIndex=n,load,capacity=t.BarLength,actual=Snapshot(t,r)});}}
 if(r.Patterns.Sum(p=>p.Multiplicity)!=r.Bars.Count)Emit("pattern-multiplicity-fail",n);
}
Emit("matrix-summary",new{cases=3000,mismatches,physical,notOptimal,elapsedMs=watch.ElapsedMilliseconds,peakBytes=Process.GetCurrentProcess().PeakWorkingSet64});
// Repeat selected deterministic case for evidence consistency and clean cancellation/retry.
var reference=Make(1500,0,(600,4),(1000,3));for(int n=0;n<3;n++)Emit("repeat",Snapshot(reference,engine.Solve(reference,Opt())));
using(var cts=new CancellationTokenSource()){cts.Cancel();try{engine.Solve(reference,Opt(),cancellationToken:cts.Token);}catch(Exception e){Emit("pre-cancel",new{type=e.GetType().Name});}Emit("retry",Snapshot(reference,engine.Solve(reference,Opt())));}
