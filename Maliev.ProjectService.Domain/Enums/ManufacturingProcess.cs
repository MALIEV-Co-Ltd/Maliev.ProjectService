namespace Maliev.ProjectService.Domain.Enums;

/// <summary>
/// Supported manufacturing processes for project parts.
/// </summary>
public enum ManufacturingProcess
{
    // --- Additive Manufacturing ---

    /// <summary>Fused Deposition Modeling (FDM) — thermoplastic filament extrusion.</summary>
    FDM = 1,

    /// <summary>Stereolithography (SLA) — photopolymer resin curing with UV laser.</summary>
    SLA = 2,

    /// <summary>Selective Laser Sintering (SLS) — powder bed fusion with laser.</summary>
    SLS = 3,

    // --- Subtractive Manufacturing ---

    /// <summary>CNC Milling — 3-axis milling from block stock.</summary>
    CNC_Milling = 10,

    /// <summary>CNC Turning — lathe operations for rotational parts.</summary>
    CNC_Turning = 11,

    /// <summary>CNC 5-Axis Milling — simultaneous 5-axis for complex geometries.</summary>
    CNC_5Axis = 12,

    // --- Sheet Metal ---

    /// <summary>Sheet metal laser cutting.</summary>
    SheetMetal_Cutting = 20,

    /// <summary>Sheet metal bending / press brake forming.</summary>
    SheetMetal_Bending = 21,

    /// <summary>Sheet metal welding and fabrication.</summary>
    SheetMetal_Welding = 22,

    // --- Other ---

    /// <summary>Injection molding — mass production via tooling.</summary>
    InjectionMolding = 30,

    /// <summary>3D scanning — reverse engineering / quality inspection.</summary>
    ThreeDScanning = 40,

    /// <summary>Design services — CAD modeling, engineering design.</summary>
    Design = 50,

    /// <summary>Assembly and finishing services.</summary>
    Assembly = 60
}
