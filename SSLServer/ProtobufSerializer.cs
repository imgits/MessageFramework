using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ProtoBuf;
using System.IO;
using ProtoBuf.Meta;

namespace SSLServer
{
    class ProtobufSerializer
    {
        static Dictionary<int, Type> MsgTypeId2Name = new Dictionary<int, Type>();
        static Dictionary<Type, int> MsgTypeName2Id = new Dictionary<Type, int>();
        static Dictionary<string,Type> MsgName2Type = new Dictionary<string,Type>();
        static ProtoBuf.Meta.RuntimeTypeModel MessageTypeModel = ProtoBuf.Meta.TypeModel.Create();
        static ProtobufSerializer()
        {
            int msgid = 0;
            Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int SubTypeFiledNumber = 1000;
            AddTypeToMessageModel<MessageHeader>(MessageTypeModel);
            foreach (Assembly asm in Assemblies)
            {
                Type[] Types = asm.GetTypes();
                foreach(var type in Types)
                {
                    if (type == typeof(MessageHeader)) continue;
                    ProtoContractAttribute[] attrs = (ProtoContractAttribute[])type.GetCustomAttributes(typeof(ProtoContractAttribute));
                    if (attrs.Length>0)
                    {
                        AddTypeToMessageModel(MessageTypeModel,type);
                        MessageTypeModel[typeof(MessageHeader)].AddSubType(SubTypeFiledNumber++, type);
                        MsgTypeId2Name[msgid] = type;
                        MsgTypeName2Id[type] = msgid++;
                        MsgName2Type[type.FullName] = type;
                    }
                }
            }

        }

        static private MetaType AddTypeToMessageModel<T>(RuntimeTypeModel typeModel)
        {
            var properties = typeof(T).GetProperties().Select(p => p.Name).OrderBy(name => name);//OrderBy added, thanks MG
            return typeModel.Add(typeof(T), true).Add(properties.ToArray());
        }

        static private MetaType AddTypeToMessageModel(RuntimeTypeModel typeModel, Type type)
        {
            var properties = type.GetProperties().Select(p => p.Name).OrderBy(name => name);//OrderBy added, thanks MG
            return typeModel.Add(type, true).Add(properties.ToArray());
        }


        public static Type MsgType(int id)
        {
            return MsgTypeId2Name[id];
        }

        public static int MsgTypeId(Type type)
        {
            return MsgTypeName2Id[type];
        }

        static public byte[] Encode<T>(T message) where T : class
        {
            try
            {
                MessageHeader messagehdr = message as MessageHeader;
                MessageHeader msghdr = new MessageHeader(messagehdr);
                msghdr.type = ProtobufSerializer.MsgTypeId(typeof(T));
                msghdr.TypeName = typeof(T).FullName;
                using (MemoryStream ms = new MemoryStream())
                {
                    //消息总长度
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    //消息头长度
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    //序列化消息头
                    ProtoBuf.Serializer.SerializeWithLengthPrefix<MessageHeader>(ms, msghdr,PrefixStyle.Fixed32);
                    int header_size = (int)ms.Length - 4;
                    //序列化消息体
                    ProtoBuf.Serializer.Serialize<T>(ms, message);
                    byte[] msg = ms.ToArray();
                    int packet_size = msg.Length;

                    msg[0] = (byte)(packet_size & 0xff);
                    msg[1] = (byte)((packet_size >> 8) & 0xff);

                    msg[2] = (byte)(header_size & 0xff);
                    msg[3] = (byte)((header_size >> 8) & 0xff);
                    return msg;
                }
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        static public T Decode<T>(byte[] buffer, int offset, int count) where T : class
        {
            T msg = null;
            try
            {
                int packet_size = BitConverter.ToUInt16(buffer, offset);
                int header_size = BitConverter.ToUInt16(buffer, offset+2);
                using (MemoryStream ms = new MemoryStream(buffer, offset+4, count-4))
                {
                    MessageHeader msghdr = ProtoBuf.Serializer.DeserializeWithLengthPrefix<MessageHeader>(ms,PrefixStyle.Fixed32);
                    msg = (T)ProtoBuf.Serializer.Deserialize<T>(ms);
                }
            }
            catch(Exception ex)
            {
                
            }
            return msg;
        }

        static public byte[] Serialize<T>(T message) where T : class
        {
            try
            {
                MessageHeader msghdr = message as MessageHeader;
                msghdr.TypeName = typeof(T).FullName;

                using (MemoryStream ms = new MemoryStream())
                {
                    //消息总长度
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    byte[] typename_bytes = Encoding.ASCII.GetBytes(msghdr.TypeName);
                    ms.Write(typename_bytes, 0, typename_bytes.Length);
                    //序列化消息
                    MessageTypeModel.Serialize(ms, message);
                    byte[] msg = ms.ToArray();
                    int packet_size = msg.Length;

                    msg[0] = (byte)(packet_size & 0xff);
                    msg[1] = (byte)((packet_size >> 8) & 0xff);
                    msg[2] = (byte)typename_bytes.Length;
                    return msg;
                }
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        static public T Deserialize<T>(byte[] buffer, int offset, int count) where T : class
        {
            T msg = null;
            try
            {
                int packet_size = BitConverter.ToUInt16(buffer, offset);
                int typename_size = buffer[2];
                string TypeName = Encoding.ASCII.GetString(buffer, 3, typename_size);
                int prefix_size = 2 + 1 + typename_size;
                
                using (MemoryStream ms = new MemoryStream(buffer, offset+ prefix_size, count- prefix_size))
                {
                    msg = (T)MessageTypeModel.Deserialize(ms,null,typeof(T));
                }
            }
            catch (Exception ex)
            {

            }
            return msg;
        }

        static public MessageHeader Deserialize(byte[] buffer, int offset, int count)
        {
            MessageHeader msg = null;
            try
            {
                int packet_size = BitConverter.ToUInt16(buffer, offset);
                int typename_size = buffer[2];
                string TypeName = Encoding.ASCII.GetString(buffer, 3, typename_size);
                int prefix_size = 2 + 1 + typename_size;
                Type MsgType = Type.GetType(TypeName);
                using (MemoryStream ms = new MemoryStream(buffer, offset + prefix_size, count - prefix_size))
                {
                    msg = (MessageHeader)MessageTypeModel.Deserialize(ms, null, MsgType);
                }
            }
            catch (Exception ex)
            {

            }
            return msg;
        }

    }
}
