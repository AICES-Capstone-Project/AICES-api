using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class RoleService : IRoleService
    {
        private readonly IUnitOfWork _uow;

        public RoleService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> GetAllAsync()
        {
            var roleRepo = _uow.GetRepository<IRoleRepository>();
            var roles = await roleRepo.GetAllAsync();
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
            var roleRepo = _uow.GetRepository<IRoleRepository>();
            var role = await roleRepo.GetByIdAsync(id);
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




