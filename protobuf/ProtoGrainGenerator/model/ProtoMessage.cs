// -----------------------------------------------------------------------
// <copyright file="ProtoMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace ProtoBuf
{
    public class ProtoMessage
    {
        public string Name { get; set; } = null!;

        public ProtoField[] Fields { get; set; } = null!;
    }
}