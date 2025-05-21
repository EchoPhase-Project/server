using System;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace EchoPhase.Roles
{
	public class UserRole : IdentityRole<Guid>
	{
		public UserRole() : base()
        { }
        public UserRole(string name) : base(name)
        { }

        public string? RoleDescription { get; set; }
        public string? RoleName { get; set; }
        
        public int RoleID { get; set; }
	}
}
