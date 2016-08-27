using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Runtime.InteropServices;

namespace Gw2GraphicalOverall {
    class MumbleLink : IDisposable {
        const string MumbleLinkFile = "MumbleLink";
        static readonly int LinkedMemSize = Marshal.SizeOf(typeof(LinkedMem));

        MemoryMappedFile f;
        MemoryMappedViewAccessor view;

        private MumbleLink(MemoryMappedFile f) {
            this.f = f;
            this.view = f.CreateViewAccessor();
        }

        public static MumbleLink Open() {
            return new MumbleLink(MemoryMappedFile.CreateOrOpen("MumbleLink", LinkedMemSize));
        }

        public void Read(out LinkedMem state, out GW2Context context) {
            state = UnmarshalRead<LinkedMem>(view);
            context = UnmarshalRead<GW2Context>(state.context);
        }

        static T UnmarshalRead<T>(MemoryMappedViewAccessor view) where T : struct {
            byte[] data = new byte[Marshal.SizeOf(typeof(LinkedMem))];
            view.ReadArray(0, data, 0, data.Length);
            return UnmarshalRead<T>(data);
        }

        static T UnmarshalRead<T>(byte[] data) where T : struct {
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try {
                return (T)Marshal.PtrToStructure(pin.AddrOfPinnedObject(), typeof(T));
            } finally {
                pin.Free();
            }
        }

        /* From https://github.com/arenanet/api-cdi/blob/master/mumble.md */
        [StructLayout(LayoutKind.Sequential)]
        public struct GW2Context {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
            public byte[] serverAddress; // contains sockaddr_in or sockaddr_in6
            public uint mapId;
            public uint mapType;
            public uint shardId;
            public uint instance;
            public uint buildId;
        }

        /* From http://wiki.mumble.info/wiki/Link */
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LinkedMem {
            public UInt32 uiVersion;
            public UInt32 uiTick;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fAvatarPosition;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fAvatarFront;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fAvatarTop;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fCameraPosition;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fCameraFront;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] fCameraTop;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string identity;
            public UInt32 context_len;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] context;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
            public string description;
        };

        public void Dispose() {
            f.Dispose();
        }
    }
}
