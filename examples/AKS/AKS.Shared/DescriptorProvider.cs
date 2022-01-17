using aks.messages;
using Google.Protobuf.Reflection;

namespace AKS.Shared
{
    public class DescriptorProvider : IDescriptorProvider
    {
        public FileDescriptor[] GetDescriptors() => new FileDescriptor[] { AKSReflection.Descriptor };
    }
}
