using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Context
{
	public class AuthContext:
		DbContext
	{
		public DbSet<User> Users { get; set; }

		public AuthContext(DbContextOptions<AuthContext> options) 
			: base(options)
		{
		} 

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.HasDefaultSchema("auth");
		}
	}
}
