using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IProfileService
    {
        Task<Profile> CreateDefaultProfileAsync(int userId, string fullName);
    }
}

