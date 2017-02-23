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
    public class ProtobufSerializer
    {
        static Dictionary<int, Type> MsgTypeId2Type = new Dictionary<int, Type>();
        //static Dictionary<Type, int> MsgTypeName2Id = new Dictionary<Type, int>();

        static ProtoBuf.Meta.RuntimeTypeModel MessageTypeModel = ProtoBuf.Meta.TypeModel.Create();

        static ProtobufSerializer()
        {
            try
            {
                Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
                int SubTypeFiledNumber = 1000;
                AddTypeToMessageModel<MessageHeader>(MessageTypeModel);
                foreach (Assembly asm in Assemblies)
                {
                    Type[] Types = asm.GetTypes();
                    foreach (var type in Types)
                    {
                        if (type == typeof(MessageHeader)) continue;
                        ProtoContractAttribute[] attrs = (ProtoContractAttribute[])type.GetCustomAttributes(typeof(ProtoContractAttribute));
                        if (attrs.Length > 0)
                        {
                            AddTypeToMessageModel(MessageTypeModel, type);
                            if (type.BaseType == typeof(MessageHeader))
                            {
                                MessageTypeModel[typeof(MessageHeader)].AddSubType(SubTypeFiledNumber++, type);
                                int typeid = type.AssemblyQualifiedName.GetHashCode();
                                if (MsgTypeId2Type.ContainsKey(typeid))
                                {
                                    Type oldtype = MsgTypeId2Type[typeid];
                                    throw new Exception($"Message type({oldtype.FullName}) has same HASH value as the message type({type.FullName}),please modify one of the names");
                                }
                                MsgTypeId2Type[typeid] = type;
                            }

                        }
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
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
                int typeid = typeof(T).AssemblyQualifiedName.GetHashCode();
                string TypeName = typeof(T).FullName + "," + "MessagesDefine";
                using (MemoryStream ms = new MemoryStream())
                {
                    //消息总长度
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    //消息类型id
                    ms.WriteByte((byte)(typeid & 0xff));
                    ms.WriteByte((byte)((typeid>>8) & 0xff));
                    ms.WriteByte((byte)((typeid >>16) & 0xff));
                    ms.WriteByte((byte)((typeid >>24) & 0xff));
                    //序列化消息
                    MessageTypeModel.Serialize(ms, message);
                    byte[] msg = ms.ToArray();
                    int packet_size = msg.Length;

                    msg[0] = (byte)(packet_size & 0xff);
                    msg[1] = (byte)((packet_size >> 8) & 0xff);
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
                //消息类型id
                int typeid = BitConverter.ToInt32(buffer, offset + 2);
                Type MsgType = null;
                if (MsgTypeId2Type.ContainsKey(typeid))
                {
                    MsgType = MsgTypeId2Type[typeid];
                }
                else
                {
                    throw new Exception("Undefined message type");
                }
                int prefix_size = 2 + 4;
                
                using (MemoryStream ms = new MemoryStream(buffer, offset+ prefix_size, count- prefix_size))
                {
                    msg = MessageTypeModel.Deserialize(ms,null, MsgType) as MessageHeader;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return msg;
        }
    }
}
