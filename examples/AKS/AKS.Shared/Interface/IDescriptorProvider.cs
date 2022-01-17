using Google.Protobuf.Reflection;

namespace AKS.Shared
{
    public interface IDescriptorProvider
    {
        FileDescriptor[] GetDescriptors();
    }
}