namespace Digital_Scholarship_Management_System_DDAC.Models;

// Single source of truth for the 4 required application document types,
// used by the Apply/Edit forms, the student tracker, and the Provider's
// application review screen.
public static class DocumentTypeCatalog
{
    public const string Transcript = "Transcript";
    public const string IncomeProof = "IncomeProof";
    public const string Certificate = "Certificate";
    public const string IdCard = "IDCard";

    public static readonly IReadOnlyList<(string Code, string Label)> All = new List<(string, string)>
    {
        (Transcript, "Academic Transcript"),
        (IncomeProof, "Proof of Household Income"),
        (Certificate, "Achievement Certificate"),
        (IdCard, "Identity Card"),
    };

    public static string GetLabel(string code) => All.FirstOrDefault(x => x.Code == code).Label ?? code;
}
