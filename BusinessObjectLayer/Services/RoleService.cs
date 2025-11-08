using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var roles = await _roleRepository.GetAllAsync();
            var result = roles.Select(r => new RoleResponse
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName
            }).ToList();

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Roles retrieved successfully.",
                Data = result
            };
        }

        public async Task<ServiceResponse> GetByIdAsync(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);
            if (role == null)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Role not found."
                };
            }

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Role retrieved successfully.",
                Data = new RoleResponse
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName
                }
            };
        }
    }
}




