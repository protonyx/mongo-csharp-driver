﻿/* Copyright 2013-2014 MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Core.WireProtocol.Messages.Encoders.BinaryEncoders
{
    public class UpdateMessageBinaryEncoder : MessageBinaryEncoderBase, IMessageEncoder<UpdateMessage>
    {
        #region static
        // static fields
        private static readonly UTF8Encoding __strictEncoding = new UTF8Encoding(false, true);
        #endregion

        // constructors
        public UpdateMessageBinaryEncoder(Stream stream, MessageEncoderSettings encoderSettings)
            : base(stream, encoderSettings)
        {
        }

        // methods
        private UpdateFlags BuildUpdateFlags(UpdateMessage message)
        {
            var flags = UpdateFlags.None;
            if (message.IsMulti)
            {
                flags |= UpdateFlags.Multi;
            }
            if (message.IsUpsert)
            {
                flags |= UpdateFlags.Upsert;
            }
            return flags;
        }

        public UpdateMessage ReadMessage()
        {
            var binaryReader = CreateBinaryReader();
            var streamReader = binaryReader.StreamReader;

            var messageSize = streamReader.ReadInt32();
            var requestId = streamReader.ReadInt32();
            var responseTo = streamReader.ReadInt32();
            var opcode = (Opcode)streamReader.ReadInt32();
            var reserved = streamReader.ReadInt32();
            var fullCollectionName = streamReader.ReadCString();
            var flags = (UpdateFlags)streamReader.ReadInt32();
            var context = BsonDeserializationContext.CreateRoot<BsonDocument>(binaryReader);
            var query = BsonDocumentSerializer.Instance.Deserialize(context);
            var update = BsonDocumentSerializer.Instance.Deserialize(context);

            var isMulti = flags.HasFlag(UpdateFlags.Multi);
            var isUpsert = flags.HasFlag(UpdateFlags.Upsert);

            return new UpdateMessage(
                requestId,
                CollectionNamespace.FromFullName(fullCollectionName),
                query,
                update,
                NoOpElementNameValidator.Instance,
                isMulti,
                isUpsert);
        }

        public void WriteMessage(UpdateMessage message)
        {
            Ensure.IsNotNull(message, "message");

            var binaryWriter = CreateBinaryWriter();
            var streamWriter = binaryWriter.StreamWriter;
            var startPosition = streamWriter.Position;

            streamWriter.WriteInt32(0); // messageSize
            streamWriter.WriteInt32(message.RequestId);
            streamWriter.WriteInt32(0); // responseTo
            streamWriter.WriteInt32((int)Opcode.Update);
            streamWriter.WriteInt32(0); // reserved
            streamWriter.WriteCString(message.CollectionNamespace.FullName);
            streamWriter.WriteInt32((int)BuildUpdateFlags(message));
            var context = BsonSerializationContext.CreateRoot<BsonDocument>(binaryWriter);
            BsonDocumentSerializer.Instance.Serialize(context, message.Query ?? new BsonDocument());
            BsonDocumentSerializer.Instance.Serialize(context, message.Update ?? new BsonDocument());
            streamWriter.BackpatchSize(startPosition);
        }

        // explicit interface implementations
        MongoDBMessage IMessageEncoder.ReadMessage()
        {
            return ReadMessage();
        }

        void IMessageEncoder.WriteMessage(MongoDBMessage message)
        {
            WriteMessage((UpdateMessage)message);
        }

        // nested types
        [Flags]
        public enum UpdateFlags
        {
            None = 0,
            Upsert = 1,
            Multi = 2
        }
    }
}
