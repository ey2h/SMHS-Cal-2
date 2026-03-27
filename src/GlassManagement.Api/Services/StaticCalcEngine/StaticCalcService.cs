namespace GlassManagement.Api.Services.StaticCalcEngine;

public class StaticCalcService
{
    public BeamCalcResult Calculate(BeamCalcRequest request)
    {
        var result = request.Support switch
        {
            SupportCondition.SimpleBeam => SimpleBeamCalc.Calculate(request),
            SupportCondition.Cantilever => CantileverBeamCalc.Calculate(request),
            SupportCondition.DoubleSpan => DoubleSpanBeamCalc.Calculate(request),
            SupportCondition.ContinuousThreeSpan => ContinuousBeamCalc.Calculate(request),
            SupportCondition.MultiSpanHingedStatical => MultiSpanHingedCalc.Calculate(request, false),
            SupportCondition.MultiSpanHingedIndeterminate => MultiSpanHingedCalc.Calculate(request, true),
            SupportCondition.OverhangBeam => OverhangBeamCalc.Calculate(request),
            _ => SimpleBeamCalc.Calculate(request)
        };

        BeamDiagramGenerator.Attach(request, result);
        return result;
    }

    public List<MechanicalModel> GetModels()
    {
        return new List<MechanicalModel>
        {
            new() { Id = "SB-U", Name = "Simple / Uniform", Support = SupportCondition.SimpleBeam, Load = LoadType.Uniform, Reference = "Handbook", Description = "Simple beam under uniform load" },
            new() { Id = "SB-C", Name = "Simple / Concentrated", Support = SupportCondition.SimpleBeam, Load = LoadType.Concentrated, Reference = "Handbook", Description = "Simple beam under concentrated load" },
            new() { Id = "CL-U", Name = "Cantilever / Uniform", Support = SupportCondition.Cantilever, Load = LoadType.Uniform, Reference = "Handbook", Description = "Cantilever beam under uniform load" },
            new() { Id = "DS-U", Name = "Double span / Uniform", Support = SupportCondition.DoubleSpan, Load = LoadType.Uniform, Reference = "Handbook", Description = "Double-span continuous beam" },
            new() { Id = "TS-U", Name = "Three span / Uniform", Support = SupportCondition.ContinuousThreeSpan, Load = LoadType.Uniform, Reference = "Handbook", Description = "Three-span continuous beam" },
            new() { Id = "MH-U", Name = "Multi-span hinged", Support = SupportCondition.MultiSpanHingedStatical, Load = LoadType.Uniform, Reference = "Handbook", Description = "Statically determinate hinged multi-span beam" },
            new() { Id = "OH-U", Name = "Overhang / Uniform", Support = SupportCondition.OverhangBeam, Load = LoadType.Uniform, Reference = "Handbook", Description = "Simple beam with overhang" }
        };
    }
}
