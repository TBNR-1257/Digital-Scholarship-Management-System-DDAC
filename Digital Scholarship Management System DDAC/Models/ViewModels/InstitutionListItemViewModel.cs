namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class InstitutionListItemViewModel
    {
        public int Id { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
        public DateTime? ModeratedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public string? RejectionReason { get; set; }
    }
}
