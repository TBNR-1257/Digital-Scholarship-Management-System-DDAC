namespace Digital_Scholarship_Management_System_DDAC.Models.ViewModels
{
    public class ApplicationTrackerViewModel
    {
        public int ApplicationId { get; set; }
        public string ScholarshipTitle { get; set; } = string.Empty;
        public string Status { get; set; } = "Submitted";
        public DateTime? SubmittedAt { get; set; }
        public List<ApplicationDocumentViewModel> Documents { get; set; } = new();

        public bool CanEdit => Status != "Approved" && Status != "Rejected";
    }
}
