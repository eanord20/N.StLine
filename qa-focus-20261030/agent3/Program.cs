using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using NStLine.App.Services;
using NStLine.App.Models;
using NStLine.App.ViewModels;
using NStLine.Core.Models;
using NStLine.Core.Contracts;
using NStLine.Cutting.Services;

class Program
{
 static readonly List<object> Records = new();
 static void Record(string scenario, object data) { Records.Add(new {scenario,data}); }
 [STAThread] static void Main(string[] args)
 {
  if(args.Length==0) {
   var info=new System.Diagnostics.ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add(typeof(Program).Assembly.Location);info.ArgumentList.Add("child");
   using var p=System.Diagnostics.Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();var watch=System.Diagnostics.Stopwatch.StartNew();long peak=0;string reason="completed";
   while(!p.WaitForExit(50)) {
    long memory;
    try { p.Refresh(); if(p.HasExited) break; memory=p.WorkingSet64; }
    catch(InvalidOperationException) { if(p.HasExited) break; throw; }
    peak=Math.Max(peak,memory);
    if(watch.Elapsed.TotalSeconds>30||memory>400L*1024*1024) {
     reason="watchdog";
     try { if(!p.HasExited) p.Kill(true); } catch(InvalidOperationException) { if(!p.HasExited) throw; }
     break;
    }
   }
   p.WaitForExit();File.WriteAllText("evidence-stdout.txt",stdout.GetAwaiter().GetResult());File.WriteAllText("evidence-stderr.txt",stderr.GetAwaiter().GetResult());var summary=$"exit={p.ExitCode} elapsed={watch.Elapsed.TotalSeconds} peakWorkingSet={peak} reason={reason}";File.WriteAllText("watchdog.txt",summary);Console.WriteLine(summary);return;
  }
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  foreach(var section in new (string name,Action run)[]{("parser",Parser),("engine",Engine),("vm",Vm)}) {
   try { section.run(); } catch(Exception e) { Record("HARNESS_ERROR",new{section=section.name,error=e.ToString()}); }
  }
  var options=new JsonSerializerOptions {WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"..","..","..","evidence-rerun.json"),JsonSerializer.Serialize(Records,options));
  Console.WriteLine(JsonSerializer.Serialize(Records,options));
 }
 static void Parser()
 {
  foreach(var culture in new[]{"en-US","uk-UA"}) {
   CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
   foreach(var cells in new[]{new[]{"P","1000","0"},new[]{"P","1000","-7"},new[]{"P","1000","1.5"},new[]{"P","1000","0.1"},new[]{"P","1000","2147483648"},new[]{"P","1000","1e100"},new[]{"P","1000","Infinity"},new[]{"P","1000","NaN"},new[]{"1250","abc"},new[]{"1250","1.5"},new[]{"P","1000","   "},new[]{"  P  "," 1250 "," 2 "},new[]{"P","NaN","2"},new[]{"P","Infinity","2"},new[]{"P","1,5","2"},Array.Empty<string>(),new[]{"   "}}) {
    var ok=PartPasteParser.TryParse(cells,out var name,out var length,out var quantity);
    Record("parser",new {culture,cells,ok,name,length,quantity});
   }
  }
  CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
 }
 static CuttingTask TaskFor(double bar,double cut,params (double l,int q)[] items)=>new(){Material=new LinearMaterial{Id="m",Name="M",StandardLength=bar,CutWidth=cut},BarLength=bar,CutWidth=cut,Parts=items.Select((x,i)=>new PartRequirement{Id="p"+i,MaterialId="m",Name="P"+i,Length=x.l,Quantity=x.q}).ToArray()};
 static void Engine()
 {
  foreach(var (id,task) in new (string,CuttingTask)[]{("zero",TaskFor(6000,3,(0,1))),("negative",TaskFor(6000,3,(-1,1))),("nan",TaskFor(6000,3,(double.NaN,1))),("inf",TaskFor(6000,3,(double.PositiveInfinity,1))),("empty",TaskFor(6000,3)),("qty0",TaskFor(6000,3,(1000,0))),("cutEqual",TaskFor(10,10,(1,1))),("impossible",TaskFor(6000,3,(5998,1))),("exactFit",TaskFor(6000,3,(5997,1))),("tiny",TaskFor(1e-10,0,(6e-11,2))),("nearFit",TaskFor(1,0,(0.5000000002,2))),("hugeFinite",TaskFor(1e308,0,(6e307,2))),("tinyRatio",TaskFor(6000,0,(1e-300,1)))}) {
   try { var r=new CuttingEngine().Solve(task,new CuttingOptions()); Record("engine",new{id,bar=task.BarLength,cut=task.CutWidth,input=task.Parts.Select(p=>new{p.Length,p.Quantity}),status=r.Status.ToString(),r.TotalBarCount,r.TotalRemainder,r.TotalPartLength,r.TotalBarLength,r.UtilizationPercent,bars=r.Bars.Select(b=>new{count=b.Parts.Count,b.Remainder,b.CutLoss,actualUsed=b.Parts.Sum(p=>p.Source.Length)+b.CutLoss})}); }
   catch(Exception e) {Record("engine",new{id,exception=e.GetType().Name,e.Message});}
  }
  // Independent exhaustive copy placement using decimal, not candidate pattern generation.
  var rng=new Random(3030); var mismatches=new List<object>();
  for(int n=0;n<150;n++) {
   decimal bar=rng.Next(5,31)/10m,cut=rng.Next(0,3)/10m;
   var lengths=Enumerable.Range(0,rng.Next(1,8)).Select(_=>rng.Next(1,(int)((bar-cut)*10)+1)/10m).OrderByDescending(x=>x).ToArray();
   int oracle=Oracle(lengths,bar,cut);
   var task=TaskFor((double)bar,(double)cut,lengths.Select(x=>((double)x,1)).ToArray());
   try {
    var r=new CuttingEngine().Solve(task,new CuttingOptions{TimeLimitSeconds=1,MaxPatternCount=10000});
    if(r.TotalBarCount!=oracle || r.Bars.Any(b=>b.Parts.Sum(p=>(decimal)p.Source.Length)+(decimal)b.CutLoss>bar)) mismatches.Add(new{n,bar,cut,lengths,oracle,actual=r.TotalBarCount,status=r.Status.ToString()});
   } catch(Exception e) { Record("oracleRejected",new{n,bar,cut,lengths,oracle,error=e.ToString()}); }
  }
  Record("oracle150",new{seed=3030,cases=150,mismatches});
  var cancelled=new CancellationToken(true);try {new CuttingEngine().Solve(TaskFor(6000,3,(1000,1)),new CuttingOptions(),null,cancelled);Record("preCancel","unexpected result");}catch(Exception e){Record("preCancel",e.GetType().Name);}
 }
 static int Oracle(decimal[] lengths,decimal bar,decimal cut) {
  int best=lengths.Length; var used=new List<decimal>();
  void Search(int i) { if(i==lengths.Length){best=Math.Min(best,used.Count);return;} if(used.Count>=best)return;
   var seen=new HashSet<decimal>();for(int j=0;j<used.Count;j++)if(seen.Add(used[j])&&used[j]+lengths[i]+cut<=bar){used[j]+=lengths[i]+cut;Search(i+1);used[j]-=lengths[i]+cut;}
   used.Add(lengths[i]+cut);Search(i+1);used.RemoveAt(used.Count-1);
  } Search(0);return best;
 }
 static MainViewModel Make(IProjectCuttingEngine engine) {
  var m=new LinearMaterial{Id="m",Name="M",StandardLength=6000,CutWidth=3};
  var vm=new MainViewModel(engine,new MemoryLibrary(m),null,new MemorySettings(),null,_=>false);
  vm.AddMaterialCommand.Execute(m);vm.AddRowCommand.Execute(null);vm.Parts[0].Length=1000;return vm;
 }
 static void Pump(Task task) { var frame=new DispatcherFrame();task.ContinueWith(_=>Dispatcher.CurrentDispatcher);var timer=new DispatcherTimer(TimeSpan.FromMilliseconds(10),DispatcherPriority.Background,(_,_)=>{if(task.IsCompleted)frame.Continue=false;},Dispatcher.CurrentDispatcher);timer.Start();Dispatcher.PushFrame(frame);timer.Stop();task.GetAwaiter().GetResult(); }
 static void Vm() {
  var vm=Make(new ProjectCuttingEngine(new CuttingEngine()));
  foreach(var value in new[]{0d,-1d,double.NaN,double.PositiveInfinity,1e-300,5997,5998}) {vm.Parts[0].Length=value;Record("vmValidation",new{value,vm.Parts[0].Error,canCalculate=vm.CalculateCommand.CanExecute(null)});}
  vm.Parts[0].Length=1000;vm.Parts[0].Quantity=int.MaxValue;vm.IncreaseQuantityCommand.Execute(vm.Parts[0]);Record("quantityPlus",new{input=int.MaxValue,actual=vm.Parts[0].Quantity,vm.Parts[0].Error});
  vm.Parts[0].Quantity=1;Pump(vm.CalculateAsync());vm.Parts[0].Length=1200;Record("afterEdit",new{vm.IsCurrentResultStale,copyEnabled=vm.CopyResultsCommand.CanExecute(null)});
  var gate=new GateEngine();var race=Make(gate);var work=race.CalculateAsync();if(!gate.Started.Wait(3000))throw new Exception("gate start timeout");
  Record("duringCalculation",new{race.IsBusy,calculateEnabled=race.CalculateCommand.CanExecute(null),cancelEnabled=race.CancelCalculationCommand.CanExecute(null)});
  race.Parts[0].Length=2000;gate.Release.Set();Pump(work);Record("editDuringCalculation",new{liveLength=race.Parts[0].Length,snapshotLength=race.CurrentTask!.Parts[0].Length,race.IsCurrentResultStale,copyEnabled=race.CopyResultsCommand.CanExecute(null),totalPartLength=race.CurrentResult!.Materials[0].Result.TotalPartLength});
  var gate2=new GateEngine();var cancel=Make(gate2);var w=cancel.CalculateAsync();if(!gate2.Started.Wait(3000))throw new Exception("gate2 timeout");cancel.CancelCalculationCommand.Execute(null);cancel.CancelCalculationCommand.Execute(null);gate2.Release.Set();Pump(w);Record("cancelTwice",new{hasResult=cancel.CurrentResult!=null,cancel.IsBusy,cancel.StatusText});
 }
 sealed class MemoryLibrary(LinearMaterial m):IMaterialsLibraryService {public IReadOnlyList<LinearMaterial> LoadAll()=>new[]{m};public void SaveAll(IEnumerable<LinearMaterial> materials){} }
 sealed class MemorySettings:IAppSettingsService {public AppSettings Load()=>new(){Theme="invalid",Culture="en",ReportOutputPath=""};public void Save(AppSettings settings){} }
 sealed class GateEngine:IProjectCuttingEngine {
  public ManualResetEventSlim Started=new(),Release=new();
  public ProjectCuttingResult Solve(ProjectCuttingTask task,CuttingOptions options,IProgress<double>? progress=null,CancellationToken cancellationToken=default) {Started.Set();if(!Release.Wait(5000))throw new TimeoutException();return new ProjectCuttingEngine(new CuttingEngine()).Solve(task,options,progress,cancellationToken);}
 }
}
