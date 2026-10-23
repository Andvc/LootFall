using System.Linq;
using Godot;
using LootFall.Debug;

namespace LootFall;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--sim"))
        {
            int code = 0;
            try
            {
                Simulation.Report(int.Parse(Arg(args, "--n") ?? "12"), double.Parse(Arg(args, "--hours") ?? "6", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (System.Exception e)
            {
                GD.PrintErr(e.ToString());
                code = 1;
            }
            GetTree().Quit(code);
        }
    }

    static string Arg(string[] args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];
}
