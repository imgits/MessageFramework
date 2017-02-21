using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ProtoBuf;
using System.IO;
using ProtoBuf.Meta;

namespace MessageFramework
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

        /// <summary>
        /// 将类T添加到序列化Model中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="typeModel"></param>
        /// <returns></returns>
        static private MetaType AddTypeToMessageModel<T>(RuntimeTypeModel typeModel)
        {
            var properties = typeof(T).GetProperties().Select(p => p.Name).OrderBy(name => name);//OrderBy added, thanks MG
            return typeModel.Add(typeof(T), true).Add(properties.ToArray());
        }

        /// <summary>
        /// 将类type添加到序列化Model中
        /// </summary>
        /// <param name="typeModel"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static private MetaType AddTypeToMessageModel(RuntimeTypeModel typeModel, Type type)
        {
            var properties = type.GetProperties().Select(p => p.Name).OrderBy(name => name);//OrderBy added, thanks MG
            return typeModel.Add(type, true).Add(properties.ToArray());
        }

        /// <summary>
        /// 序列化T类型消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <returns></returns>
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
                    //消息类型名称长度(1-255)
                    ms.WriteByte(0);
                    //消息类型名称
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

        /// <summary>
        /// 序列化消息,消息类型由类名称确定
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        static public MessageHeader Deserialize(byte[] buffer, int offset, int count)
        {
            MessageHeader msg = null;
            try
            {
                //消息长度
                int packet_size = BitConverter.ToUInt16(buffer, offset);
                //消息类型名称长度(1-255)
                int typename_bytes = buffer[2];
                //消息类型名称
                string TypeName = Encoding.ASCII.GetString(buffer, 3, typename_bytes);
                Type MsgType = Type.GetType(TypeName);
                int prefix_size = 2 + 1 + typename_bytes;
                
                using (MemoryStream ms = new MemoryStream(buffer, offset+ prefix_size, count- prefix_size))
                {
                    msg = MessageTypeModel.Deserialize(ms,null, MsgType) as MessageHeader;
                }
            }
            catch (Exception ex)
            {

            }
            return msg;
        }
    }
}
