using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using LootFall.Core;
using LootFall.Debug;

// dotnet run --project tools/simcli -c Release -- [--n=12] [--hours=6]      the balance report
// dotnet run --project tools/simcli -c Release -- --tune [--rounds=3]       searches Balance for the pacing targets below
string Arg(string name) => args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];

if (!args.Contains("--tune"))
{
    Simulation.Report(int.Parse(Arg("--n") ?? "12"), double.Parse(Arg("--hours") ?? "6", CultureInfo.InvariantCulture));
    return;
}

// Minutes at which a sensible player should first reach each stage (see docs/design.md).
int[] stages = { 10, 20, 30, 40, 50, 60, 70, 80 };
double[] target = { 5, 13, 28, 50, 80, 120, 165, 220 };

// The knobs the tuner may turn, with their bounds.
var knobs = new (string Name, double Min, double Max)[]
{
    ("ChestLevelCost", 80, 600), ("ChestLevelGrowth", 1.35, 1.9), ("GoldGrowth", 1.05, 1.13), ("TrainGrowth", 1.1, 1.25),
};
FieldInfo F(string name) => typeof(Balance).GetField(name, BindingFlags.Public | BindingFlags.Static);
double Get(string name) => (double)F(name).GetValue(null);
void Set(string name, double v) => F(name).SetValue(null, v);

double Score()
{
    var got = Simulation.StageMinutes(6, 4.5, stages);
    double e = 0;
    for (int i = 0; i < stages.Length; i++)
        e += double.IsNaN(got[i]) ? 4 : Math.Pow(Math.Log(got[i] / target[i]), 2);
    return e;
}

string Show() => string.Join("  ", knobs.Select(k => $"{k.Name}={Get(k.Name):0.###}"));
double best = Score();
Console.WriteLine($"start {best:0.000}  {Show()}");
int rounds = int.Parse(Arg("--rounds") ?? "3");
for (int round = 0; round < rounds; round++)
{
    double step = 0.12 / (round + 1);
    foreach (var k in knobs)
    {
        foreach (var dir in new[] { 1.0, -1.0 })
        {
            for (int tries = 0; tries < 6; tries++)
            {
                double old = Get(k.Name);
                double v = Math.Clamp(k.Name.Contains("Growth") ? 1 + (old - 1) * (1 + dir * step) : old * (1 + dir * step * 2), k.Min, k.Max);
                Set(k.Name, v);
                double s = Score();
                if (s < best)
                {
                    best = s;
                    Console.WriteLine($"  {best:0.000}  {Show()}");
                }
                else
                {
                    Set(k.Name, old);
                    break;
                }
            }
        }
    }
}
var final = Simulation.StageMinutes(8, 5, stages);
Console.WriteLine($"best {best:0.000}  {Show()}");
Console.WriteLine("stage  target  got");
for (int i = 0; i < stages.Length; i++) Console.WriteLine($"{stages[i],5}  {target[i],6:0}  {final[i],5:0.0}");
