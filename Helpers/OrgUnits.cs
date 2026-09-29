namespace SafetyOpsTests.Helpers;

/// <summary>SafetyOpsApp's fixed org tree, in the order the UI lists it (parents before children).</summary>
public static class OrgUnits
{
    public const string Organization = "SafetyOps Industries";
    public const string Manufacturing = "Manufacturing Division";
    public const string NorthPlant = "North Plant";
    public const string SouthPlant = "South Plant";
    public const string Logistics = "Logistics Division";
    public const string EastWarehouse = "East Warehouse";
    public const string WestWarehouse = "West Warehouse";

    public static readonly string[] AllCodes = ["ORG", "MFG", "MFG-N", "MFG-S", "LOG", "LOG-E", "LOG-W"];
    public static readonly string[] ManufacturingSubtree = [Manufacturing, NorthPlant, SouthPlant];
}
