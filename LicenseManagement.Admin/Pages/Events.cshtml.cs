using System.Collections.Generic;
using System.Threading.Tasks;
using LicenseManagement.Application.DTOs;
using LicenseManagement.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseManagement.Admin.Pages
{
    public class EventsModel : PageModel
    {
        private readonly IAdminService _adminService;

        public EventsModel(IAdminService adminService)
        {
            _adminService = adminService;
        }

        public List<LicenseEventDto> Events { get; set; } = new();

        public async Task OnGetAsync()
        {
            Events = await _adminService.GetEventsAsync(150);
        }
    }
}
