<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>SharpDX</NuGetReference>
  <NuGetReference>SharpDX.Direct2D1</NuGetReference>
  <NuGetReference>SharpDX.Mathematics</NuGetReference>
  <Namespace>D2D = SharpDX.Direct2D1</Namespace>
  <Namespace>DWrite = SharpDX.DirectWrite</Namespace>
  <Namespace>Microsoft.Win32</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>SharpDX</Namespace>
  <Namespace>SharpDX.IO</Namespace>
  <Namespace>SharpDX.Mathematics.Interop</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>WIC = SharpDX.WIC</Namespace>
</Query>

void Main()
{
	var gif = SaveD2DBitmap(200, 100, "HELLO");
	File.WriteAllBytes(@"C:\Users\sdfly\Desktop\test.gif", gif);
	Util.Image(gif).Dump();
}

// Define other methods and classes here
byte[] SaveD2DBitmap(int width, int height, string text)
{
	using var wic = new WIC.ImagingFactory2();
	using var d2d = new D2D.Factory();
	using var wicBitmap = new WIC.Bitmap(wic, width, height, WIC.PixelFormat.Format32bppPBGRA, WIC.BitmapCreateCacheOption.CacheOnDemand);
	using var target = new D2D.WicRenderTarget(d2d, wicBitmap, new D2D.RenderTargetProperties());
	using var dwriteFactory = new SharpDX.DirectWrite.Factory();
	using var brush = new D2D.SolidColorBrush(target, Color.Yellow);
	using var encoder = new WIC.GifBitmapEncoder(wic);
	
	using var ms = new MemoryStream();
	using var dc = target.QueryInterface<D2D.DeviceContext>();
	using var bmpLayer = new D2D.Bitmap1(dc, target.PixelSize,
		new D2D.BitmapProperties1(new D2D.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
		d2d.DesktopDpi.Width, d2d.DesktopDpi.Height,
		D2D.BitmapOptions.Target));

	var r = new Random();
	encoder.Initialize(ms);

	D2D.Image oldTarget = dc.Target;
	{
		dc.Target = bmpLayer;
		dc.BeginDraw();
		dc.Clear(Color.Transparent);
		var textFormat = new DWrite.TextFormat(dwriteFactory, "Times New Roman",
			DWrite.FontWeight.Bold, 
			DWrite.FontStyle.Normal, width / text.Length);
		for (int charIndex = 0; charIndex < text.Length; ++charIndex)
		{
			using var layout = new DWrite.TextLayout(dwriteFactory, text[charIndex].ToString(), textFormat, float.MaxValue, float.MaxValue);
			var layoutSize = new Vector2(layout.Metrics.Width, layout.Metrics.Height);
			using var b2 = new D2D.LinearGradientBrush(dc, new D2D.LinearGradientBrushProperties
			{
				StartPoint = Vector2.Zero,
				EndPoint = layoutSize,
			}, new D2D.GradientStopCollection(dc, new[]
			{
					new D2D.GradientStop{ Position = 0.0f, Color = r.NextColor() },
					new D2D.GradientStop{ Position = 1.0f, Color = r.NextColor() },
				}));

			var position = new Vector2(charIndex * width / text.Length, r.NextFloat(0, height - layout.Metrics.Height));
			brush.Color = r.NextColor();
			dc.Transform =
				Matrix3x2.Translation(-layoutSize / 2) *
				Matrix3x2.Skew(r.NextFloat(0, 0.5f), r.NextFloat(0, 0.5f)) *
				//Matrix3x2.Rotation(r.NextFloat(0, MathF.PI * 2)) *
				Matrix3x2.Translation(position + layoutSize / 2);
			dc.DrawTextLayout(Vector2.Zero, layout, b2);
		}
		for (var i = 0; i < 4; ++i)
		{
			target.Transform = Matrix3x2.Identity;
			brush.Color = r.NextColor();
			target.DrawLine(
				r.NextVector2(Vector2.Zero, new Vector2(width, height)),
				r.NextVector2(Vector2.Zero, new Vector2(width, height)),
				brush, 3.0f);
		}
		target.EndDraw();
	}
	
	Color background = r.NextColor();
	for (var frameId = -10; frameId < 10; ++frameId)
	{
		dc.Target = null;
		using var displacement = new D2D.Effects.DisplacementMap(dc);
		displacement.SetInput(0, bmpLayer, true);
		displacement.Scale = (10 - Math.Abs(frameId)) * 10.0f;
		
		var turbulence = new D2D.Effects.Turbulence(dc);
		displacement.SetInputEffect(1, turbulence);

		dc.Target = oldTarget;
		dc.BeginDraw();
		dc.Clear(background);
		dc.DrawImage(displacement);
		dc.EndDraw();

		using (var frame = new WIC.BitmapFrameEncode(encoder))
		{
			frame.Initialize();
			frame.SetSize(wicBitmap.Size.Width, wicBitmap.Size.Height);

			var pixelFormat = wicBitmap.PixelFormat;
			frame.SetPixelFormat(ref pixelFormat);
			frame.WriteSource(wicBitmap);

			frame.Commit();
		}
	}

	encoder.Commit();
	return ms.ToArray();
}