﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DDD.Domain.Model.Entities.Admin;
using DDD.Infrastructur.Repositories;

namespace DDD.Domain.MainModule.Admin
{
    public interface IAdminActionRepository : IRepository<AdminAction, PageData<AdminAction>>
    {
        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="aat"></param>
        void Move(AdminAction aat);
    }
}
