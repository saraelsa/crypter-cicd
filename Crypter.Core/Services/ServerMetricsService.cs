﻿/*
 * Copyright (C) 2022 Crypter File Transfer
 * 
 * This file is part of the Crypter file transfer project.
 * 
 * Crypter is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * The Crypter source code is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * You can be released from the requirements of the aforementioned license
 * by purchasing a commercial license. Buying such a license is mandatory
 * as soon as you develop commercial activities involving the Crypter source
 * code without disclosing the source code of your own applications.
 * 
 * Contact the current copyright holder to discuss commercial license options.
 */

using Crypter.Contracts.Features.Metrics;
using Crypter.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Crypter.Core.Services
{
   public interface IServerMetricsService
   {
      Task<DiskMetricsResponse> GetAggregateDiskMetricsAsync(CancellationToken cancellationToken);
   }

   public class ServerMetricsService : IServerMetricsService
   {
      private readonly DataContext _context;
      private readonly TransferStorageSettings _transferStorageSettings;

      public ServerMetricsService(DataContext context, IOptions<TransferStorageSettings> transferStorageSettings)
      {
         _context = context;
         _transferStorageSettings = transferStorageSettings.Value;
      }

      public async Task<DiskMetricsResponse> GetAggregateDiskMetricsAsync(CancellationToken cancellationToken)
      {
         IQueryable<long> anonymousMessageSizes = _context.AnonymousMessageTransfers
            .Select(x => (long)x.Size);

         IQueryable<long> userMessageSizes = _context.UserMessageTransfers
            .Select(x => (long)x.Size);

         IQueryable<long> anonymousFileSizes = _context.AnonymousFileTransfers
            .Select(x => (long)x.Size);

         IQueryable<long> userFileSizes = _context.UserFileTransfers
            .Select(x => (long)x.Size);

         long usedBytes = await anonymousMessageSizes
            .Concat(userMessageSizes)
            .Concat(anonymousFileSizes)
            .Concat(userFileSizes)
            .SumAsync(cancellationToken);

         long allocatedBytes = _transferStorageSettings.AllocatedGB * Convert.ToInt64(Math.Pow(2, 30));
         long freeBytes = allocatedBytes - usedBytes;

         return new DiskMetricsResponse(allocatedBytes, freeBytes);
      }
   }
}
