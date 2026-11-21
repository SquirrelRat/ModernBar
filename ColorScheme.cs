using SharpDX;

namespace ModernBar
{
    public abstract class ColorScheme
    {
        public abstract Color Background { get; }
        public abstract Color Timer { get; }
        public abstract Color Ping { get; }
        public abstract Color Area { get; }
        public abstract Color TimeLeft { get; }
        public abstract Color Xph { get; }
        public abstract Color XphGetLeft { get; }
        public abstract Color Label { get; }
        public abstract Color Gold { get; }
    }

    public class DefaultColorScheme : ColorScheme
    {
        public override Color Background => new Color(0, 0, 0, 255);
        public override Color Timer => new Color(220, 190, 130, 255);
        public override Color Ping => new Color(220, 190, 130, 255);
        public override Color Area => new Color(140, 200, 255, 255);
        public override Color TimeLeft => new Color(220, 190, 130, 255);
        public override Color Xph => new Color(220, 190, 130, 255);
        public override Color XphGetLeft => new Color(220, 190, 130, 255);
        public override Color Label => new Color(200, 170, 110, 255);
        public override Color Gold => new Color(220, 190, 130, 255);
    }


    public class SolarizedDarkColorScheme : ColorScheme
    {
        public override Color Background => new Color(0x00, 0x2b, 0x36, 0xff);
        public override Color Timer => new Color(0x26, 0x8b, 0xd2, 0xff);
        public override Color Ping => new Color(0x85, 0x99, 0x00, 0xff);
        public override Color Area => new Color(0xcb, 0x4b, 0x16, 0xff);
        public override Color TimeLeft => new Color(0xdc, 0x32, 0x2f, 0xff);
        public override Color Xph => new Color(0xd3, 0x36, 0x82, 0xff);
        public override Color XphGetLeft => new Color(0x6c, 0x71, 0xc4, 0xff);
        public override Color Label => new Color(0x58, 0x6e, 0x75, 0xff);
        public override Color Gold => new Color(0xb5, 0x89, 0x00, 0xff);
    }

    public class DraculaColorScheme : ColorScheme
    {
        public override Color Background => new Color(0x28, 0x2a, 0x36, 0xff);
        public override Color Timer => new Color(0xbd, 0x93, 0xf9, 0xff);
        public override Color Ping => new Color(0x50, 0xfa, 0x7b, 0xff);
        public override Color Area => new Color(0xff, 0xb8, 0x6c, 0xff);
        public override Color TimeLeft => new Color(0xff, 0x55, 0x55, 0xff);
        public override Color Xph => new Color(0xff, 0x79, 0xc6, 0xff);
        public override Color XphGetLeft => new Color(0x8b, 0xe9, 0xfd, 0xff);
        public override Color Label => new Color(0x9a, 0xa3, 0xcc, 0xff);
        public override Color Gold => new Color(0xf1, 0xfa, 0x8c, 0xff);
    }


    public class InvertedColorScheme : ColorScheme
    {
        public override Color Background => new Color(0xE0, 0xE0, 0xE0, 0xff);
        public override Color Timer => Color.Black;
        public override Color Ping => Color.Black;
        public override Color Area => Color.Black;
        public override Color TimeLeft => Color.Black;
        public override Color Xph => Color.Black;
        public override Color XphGetLeft => Color.Black;
        public override Color Label => new Color(80, 80, 80, 255);
        public override Color Gold => Color.Black;
    }

    public class Cyberpunk2077ColorScheme : ColorScheme
    {
        public override Color Background => new Color(13, 13, 13, 255);
        public override Color Timer => new Color(140, 140, 140, 255);
        public override Color Ping => new Color(37, 225, 237, 255);
        public override Color Area => new Color(242, 242, 242, 255);
        public override Color TimeLeft => new Color(237, 29, 113, 255);
        public override Color Xph => new Color(243, 230, 0, 255);
        public override Color XphGetLeft => new Color(243, 230, 0, 255);
        public override Color Label => new Color(243, 230, 0, 255);
        public override Color Gold => new Color(242, 242, 242, 255);
    }

    public class MinecraftColorScheme : ColorScheme
    {
        public override Color Background => new Color(20, 40, 20, 255);
        public override Color Timer => new Color(76, 175, 80, 255);
        public override Color Ping => new Color(76, 175, 80, 255);
        public override Color Area => new Color(102, 187, 106, 255);
        public override Color TimeLeft => new Color(255, 153, 51, 255);
        public override Color Xph => new Color(255, 223, 0, 255);
        public override Color XphGetLeft => new Color(139, 195, 74, 255);
        public override Color Label => new Color(138, 138, 138, 255);
        public override Color Gold => new Color(212, 175, 55, 255);
    }

    public class ValorantColorScheme : ColorScheme
    {
        public override Color Background => new Color(15, 25, 35, 255);
        public override Color Timer => new Color(189, 189, 189, 255);
        public override Color Ping => new Color(23, 229, 195, 255);
        public override Color Area => new Color(236, 232, 225, 255);
        public override Color TimeLeft => new Color(255, 184, 76, 255);
        public override Color Xph => new Color(255, 70, 85, 255);
        public override Color XphGetLeft => new Color(255, 70, 85, 255);
        public override Color Label => new Color(139, 149, 161, 255);
        public override Color Gold => new Color(255, 184, 76, 255);
    }

    public class DiabloColorScheme : ColorScheme
    {
        public override Color Background => new Color(13, 13, 13, 255);
        public override Color Timer => new Color(138, 138, 138, 255);
        public override Color Ping => new Color(168, 168, 168, 255);
        public override Color Area => new Color(201, 191, 174, 255);
        public override Color TimeLeft => new Color(200, 76, 17, 255);
        public override Color Xph => new Color(178, 34, 34, 255);
        public override Color XphGetLeft => new Color(201, 162, 39, 255);
        public override Color Label => new Color(110, 110, 110, 255);
        public override Color Gold => new Color(201, 162, 39, 255);
    }

    public class MonochromeColorScheme : ColorScheme
    {
        public override Color Background => new Color(24, 24, 24, 255);
        public override Color Timer => Color.Gray;
        public override Color Ping => Color.Gray;
        public override Color Area => new Color(170, 170, 170, 255);
        public override Color TimeLeft => Color.White;
        public override Color Xph => new Color(200, 200, 200, 255);
        public override Color XphGetLeft => Color.White;
        public override Color Label => new Color(128, 128, 128, 255);
        public override Color Gold => new Color(200, 200, 200, 255);
    }

    public class CustomColorScheme : ColorScheme
    {
        private readonly ModernBarSettings _settings;

        public CustomColorScheme(ModernBarSettings settings)
        {
            _settings = settings;
        }

        public override Color Background => _settings.BackgroundColor;
        public override Color Timer => _settings.TimerColor;
        public override Color Ping => _settings.PingColor;
        public override Color Area => _settings.AreaColor;
        public override Color TimeLeft => _settings.TimeLeftColor;
        public override Color Xph => _settings.XphColor;
        public override Color XphGetLeft => _settings.XphGetLeftColor;
        public override Color Label => _settings.LabelColor;
        public override Color Gold => _settings.GoldColor;
    }
}

