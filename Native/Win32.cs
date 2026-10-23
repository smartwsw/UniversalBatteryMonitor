using System.Runtime.InteropServices;

namespace BatteryMonitor.Native;

internal static class Win32
{
    internal const int DIGCF_PRESENT = 0x02;
    internal const int DIGCF_DEVICEINTERFACE = 0x10;

    internal const uint FILE_SHARE_READ = 0x01;
    internal const uint FILE_SHARE_WRITE = 0x02;
    internal const uint OPEN_EXISTING = 3;
    internal const uint GENERIC_READ = 0x80000000;
    internal const uint GENERIC_WRITE = 0x40000000;
    internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    internal static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    internal const int ERROR_IO_PENDING = 997;
    internal const uint WAIT_OBJECT_0 = 0x00000000;
    internal const uint WAIT_TIMEOUT = 0x00000102;

    internal const int HIDP_STATUS_SUCCESS = 0x00110000;
    internal const int HidP_Input = 0;
    internal const int HidP_Output = 1;
    internal const int HidP_Feature = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDD_ATTRIBUTES
    {
        public uint Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct HIDP_VALUE_CAPS_RANGE
    {
        [FieldOffset(0)] public ushort UsageMin;
        [FieldOffset(2)] public ushort UsageMax;
        [FieldOffset(4)] public ushort StringMin;
        [FieldOffset(6)] public ushort StringMax;
        [FieldOffset(8)] public ushort DesignatorMin;
        [FieldOffset(10)] public ushort DesignatorMax;
        [FieldOffset(12)] public ushort DataIndexMin;
        [FieldOffset(14)] public ushort DataIndexMax;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct HIDP_VALUE_CAPS_NOT_RANGE
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort Reserved1;
        [FieldOffset(4)] public ushort StringIndex;
        [FieldOffset(6)] public ushort Reserved2;
        [FieldOffset(8)] public ushort DesignatorIndex;
        [FieldOffset(10)] public ushort Reserved3;
        [FieldOffset(12)] public ushort DataIndex;
        [FieldOffset(14)] public ushort Reserved4;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct HIDP_VALUE_CAPS_UNION
    {
        [FieldOffset(0)] public HIDP_VALUE_CAPS_RANGE Range;
        [FieldOffset(0)] public HIDP_VALUE_CAPS_NOT_RANGE NotRange;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDP_VALUE_CAPS
    {
        public ushort UsagePage;
        public byte ReportID;
        public byte IsAlias;
        public ushort BitField;
        public ushort LinkCollection;
        public ushort LinkUsage;
        public ushort LinkUsagePage;
        public byte IsRange;
        public byte IsStringRange;
        public byte IsDesignatorRange;
        public byte IsAbsolute;
        public byte HasNull;
        public byte Reserved;
        public ushort BitSize;
        public ushort ReportCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public ushort[] Reserved2;

        public uint UnitsExp;
        public uint Units;
        public int LogicalMin;
        public int LogicalMax;
        public int PhysicalMin;
        public int PhysicalMax;
        public HIDP_VALUE_CAPS_UNION Union;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevsW(in Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
        in Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadFile(IntPtr file, IntPtr buffer, uint numberOfBytesToRead,
        out uint numberOfBytesRead, ref OVERLAPPED overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(IntPtr file, byte[] buffer, uint numberOfBytesToWrite,
        out uint numberOfBytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateEventW(IntPtr eventAttributes, bool manualReset, bool initialState, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ResetEvent(IntPtr evt);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetOverlappedResult(IntPtr file, ref OVERLAPPED overlapped,
        out uint numberOfBytesTransferred, bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CancelIoEx(IntPtr file, ref OVERLAPPED overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CancelIo(IntPtr file);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool HidD_GetProductString(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool HidD_GetManufacturerString(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool HidD_GetSerialNumberString(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_GetFeature(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_SetFeature(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_SetOutputReport(IntPtr hidDeviceObject, byte[] buffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern int HidP_GetValueCaps(int reportType, [Out] HIDP_VALUE_CAPS[]? valueCaps,
        ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection,
        ushort usage, out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

    internal static readonly Guid HidInterfaceClassGuid = new("4D1E55B2-F16F-11CF-88CB-001111000030");
}
