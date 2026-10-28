using System;
using System.IO;
using GlassManagement.Calc.Api;

namespace GlassManagement.Calc.Demo;

/// <summary>
/// 计算 API 的命令行入口 —— 给「跨进程调用」用（Revit 插件、Python、Node 都能用）。
///
/// <para>协议与进程内 <see cref="CalcFacade"/> 完全一致，只是换成文件/管道。用法：</para>
/// <code>
/// Calc.Demo.exe --calc-api types                       # 输出全部构件类型 + 输入字段
/// Calc.Demo.exe --calc-api glass                       # 用默认算例算一个玻璃
/// Calc.Demo.exe --calc-api glass  in.json              # 参数从文件读
/// Calc.Demo.exe --calc-api glass  -                    # 参数从 stdin 读
/// Calc.Demo.exe --calc-api batch  in.json              # 批量（数组）
/// </code>
///
/// <para>退出码：0 = ok，2 = 计算失败（输出里带 error），3 = 用法错误。
/// 结果一律写到 stdout（UTF-8），宿主直接反序列化即可。</para>
/// </summary>
public static class CalcCli
{
    public static int Run(string[] args)
    {
        // args[0] = --calc-api
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法: --calc-api types | --calc-api <type> [input.json|-] | --calc-api batch <file|->");
            return 3;
        }

        var cmd = args[1];

        if (cmd.Equals("types", StringComparison.OrdinalIgnoreCase))
        {
            Console.Out.Write(CalcFacade.Catalog());
            return 0;
        }

        if (cmd.Equals("batch", StringComparison.OrdinalIgnoreCase))
        {
            var body = ReadBody(args.Length > 2 ? args[2] : "-");
            if (body is null) return 3;
            var res = CalcFacade.RunBatch(body);
            Console.Out.Write(res);
            return res.Contains("\"ok\": false") ? 2 : 0;
        }

        // 单构件：--calc-api <type> [file|-]
        var input = args.Length > 2 ? ReadBody(args[2]) : null;
        if (args.Length > 2 && input is null) return 3;

        var json = CalcFacade.Run(cmd, input);
        Console.Out.Write(json);
        return json.Contains("\"ok\": false") ? 2 : 0;
    }

    private static string? ReadBody(string pathOrDash)
    {
        try
        {
            return pathOrDash == "-" ? Console.In.ReadToEnd() : File.ReadAllText(pathOrDash);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"读取输入失败: {ex.Message}");
            return null;
        }
    }
}
