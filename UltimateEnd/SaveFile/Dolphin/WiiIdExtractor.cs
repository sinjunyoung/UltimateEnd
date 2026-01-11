using System;
using System.IO;
using System.Text;

namespace UltimateEnd.SaveFile.Dolphin
{
    public static class WiiIdExtractor
    {
        /// <summary>
        /// Wii ISO/WBFS에서 Title ID를 추출합니다.
        /// </summary>
        public static string? ExtractTitleId(string isoPath)
        {
            if (!File.Exists(isoPath))
                return null;

            string ext = Path.GetExtension(isoPath).ToLower();

            if (ext == ".wbfs")
                return ExtractFromWbfs(isoPath);
            else if (ext == ".iso" || ext == ".ciso")
                return ExtractFromIso(isoPath);
            else if (ext == ".wad")
                return ExtractFromWad(isoPath);
            else if (ext == ".gcz" || ext == ".rvz")
                return ExtractFromCompressed(isoPath);

            return null;
        }

        private static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                using var reader = new BinaryReader(stream);

                // Wii ISO: 오프셋 0x00에 Title ID (6 bytes)
                // 형식: RMCE01 (Mario Kart Wii)
                stream.Seek(0x00, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(6);

                string titleId = Encoding.ASCII.GetString(titleIdBytes);

                // 유효성 검사 (R, S로 시작하면 Wii)
                if (titleId.Length == 6 && (titleId[0] == 'R' || titleId[0] == 'S'))
                    return titleId;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromCompressed(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);

                // GCZ/RVZ 압축 포맷
                // 헤더 건너뛰고 Title ID 찾기 시도
                stream.Seek(0x00, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(6);

                string titleId = Encoding.ASCII.GetString(titleIdBytes);

                if (titleId.Length == 6 && (titleId[0] == 'R' || titleId[0] == 'S'))
                    return titleId;

                // 헤더 이후 시도
                stream.Seek(0x20, SeekOrigin.Begin);
                titleIdBytes = reader.ReadBytes(6);
                titleId = Encoding.ASCII.GetString(titleIdBytes);

                if (titleId.Length == 6 && (titleId[0] == 'R' || titleId[0] == 'S'))
                    return titleId;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromWbfs(string wbfsPath)
        {
            try
            {
                using var stream = File.OpenRead(wbfsPath);
                using var reader = new BinaryReader(stream);

                // WBFS 헤더 확인
                stream.Seek(0x00, SeekOrigin.Begin);
                byte[] magic = reader.ReadBytes(4);
                string magicStr = Encoding.ASCII.GetString(magic);

                if (magicStr != "WBFS")
                    return null;

                // WBFS는 디스크 헤더가 섹터 단위로 저장됨
                // 일반적으로 0x200 오프셋에 Game ID
                stream.Seek(0x200, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(6);

                string titleId = Encoding.ASCII.GetString(titleIdBytes);

                if (titleId.Length == 6 && (titleId[0] == 'R' || titleId[0] == 'S'))
                    return titleId;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromWad(string wadPath)
        {
            try
            {
                using var stream = File.OpenRead(wadPath);
                using var reader = new BinaryReader(stream);

                // WAD 헤더 확인
                stream.Seek(0x00, SeekOrigin.Begin);
                uint headerSize = ReadBigEndianUInt32(reader);

                // WAD는 구조가 복잡함, Title ID는 헤더 내부
                // 보통 0x1DC 오프셋 근처
                stream.Seek(0x1E0, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(8);

                // Title ID는 8바이트 (Big Endian)
                // 하위 4바이트를 ASCII로 변환
                string titleId = Encoding.ASCII.GetString(titleIdBytes, 4, 4);

                if (titleId.Length == 4)
                    return titleId + "00"; // 6자리로 맞춤

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}