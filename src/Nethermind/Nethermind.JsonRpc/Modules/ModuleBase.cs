﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Logging;
using Newtonsoft.Json;

namespace Nethermind.JsonRpc.Modules
{
    public abstract class ModuleBase : IModule
    {
        protected readonly ILogger Logger;
        protected readonly IJsonRpcConfig JsonRpcConfig;
        protected readonly IJsonSerializer JsonSerializer;

        protected ModuleBase(IConfigProvider configProvider, ILogManager logManager, IJsonSerializer jsonSerializer)
        {
            JsonSerializer = jsonSerializer;
            Logger = logManager.GetClassLogger();
            JsonRpcConfig = configProvider.GetConfig<IJsonRpcConfig>();
        }

        protected string GetJsonLog(object model)
        {
            return JsonSerializer.Serialize(model);
        }
        
        public abstract ModuleType ModuleType { get; }

        public virtual IReadOnlyCollection<JsonConverter> GetConverters()
        {
            return Array.Empty<JsonConverter>();   
        }
    }
}