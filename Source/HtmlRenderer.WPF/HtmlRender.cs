// "Therefore those skilled at the unorthodox
// are infinite as heaven and earth,
// inexhaustible as the great rivers.
// When they come to an end,
// they begin again,
// like the days and months;
// they die and are reborn,
// like the four seasons."
// 
// - Sun Tsu,
// "The Art of War"

using System;
using System.Windows;
using System.Windows.Media;
using HtmlRenderer.Core;
using HtmlRenderer.Core.Entities;
using HtmlRenderer.Core.Utils;
using HtmlRenderer.WPF.Adapters;

namespace HtmlRenderer.WPF
{
    /// <summary>
    /// Standalone static class for simple and direct HTML rendering.<br/>
    /// For WinForms UI prefer using HTML controls: <see cref="HtmlPanel"/> or <see cref="HtmlLabel"/>.<br/>
    /// For low-level control and performance consider using <see cref="HtmlContainer"/>.<br/>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>GDI vs. GDI+ text rendering</b><br/>
    /// Windows supports two text rendering technologies: GDI and GDI+.<br/> 
    /// GDI is older, has better performance and looks better on standard monitors but doesn't support alpha channel for transparency.<br/> 
    /// GDI+ is newer, device independent so work better for printers but is slower and looks worse on monitors.<br/>
    /// HtmlRender supports both GDI and GDI+ text rendering to accommodate different needs, GDI+ text rendering methods have "GdiPlus" suffix
    /// in their name where GDI do not.<br/>
    /// </para>
    /// <para>
    /// <b>Rendering to image</b><br/>
    /// See https://htmlrenderer.codeplex.com/wikipage?title=Image%20generation <br/>
    /// Because of GDI text rendering issue with alpha channel clear type text rendering rendering to image requires special handling.<br/>
    /// <u>Solid color background -</u> generate an image where the background is filled with solid color and all the html is rendered on top
    /// of the background color, GDI text rendering will be used. (RenderToImage method where the first argument is html string)<br/>
    /// <u>Image background -</u> render html on top of existing image with whatever currently exist but it cannot have transparent pixels, 
    /// GDI text rendering will be used. (RenderToImage method where the first argument is Image object)<br/>
    /// <u>Transparent background -</u> render html to empty image using GDI+ text rendering, the generated image can be transparent.
    /// Text rendering can be controlled using <see cref="TextRenderingHint"/>, note that <see cref="TextRenderingHint.ClearTypeGridFit"/>
    /// doesn't render well on transparent background. (RenderToImageGdiPlus method)<br/>
    /// </para>
    /// <para>
    /// <b>Overwrite stylesheet resolution</b><br/>
    /// Exposed by optional "stylesheetLoad" delegate argument.<br/>
    /// Invoked when a stylesheet is about to be loaded by file path or URL in 'link' element.<br/>
    /// Allows to overwrite the loaded stylesheet by providing the stylesheet data manually, or different source (file or URL) to load from.<br/>
    /// Example: The stylesheet 'href' can be non-valid URI string that is interpreted in the overwrite delegate by custom logic to pre-loaded stylesheet object<br/>
    /// If no alternative data is provided the original source will be used.<br/>
    /// </para>
    /// <para>
    /// <b>Overwrite image resolution</b><br/>
    /// Exposed by optional "imageLoad" delegate argument.<br/>
    /// Invoked when an image is about to be loaded by file path, URL or inline data in 'img' element or background-image CSS style.<br/>
    /// Allows to overwrite the loaded image by providing the image object manually, or different source (file or URL) to load from.<br/>
    /// Example: image 'src' can be non-valid string that is interpreted in the overwrite delegate by custom logic to resource image object<br/>
    /// Example: image 'src' in the html is relative - the overwrite intercepts the load and provide full source URL to load the image from<br/>
    /// Example: image download requires authentication - the overwrite intercepts the load, downloads the image to disk using custom code and provide 
    /// file path to load the image from.<br/>
    /// If no alternative data is provided the original source will be used.<br/>
    /// Note: Cannot use asynchronous scheme overwrite scheme.<br/>
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>
    /// <b>Simple rendering</b><br/>
    /// HtmlRender.Render(g, "<![CDATA[<div>Hello <b>World</b></div>]]>");<br/>
    /// HtmlRender.Render(g, "<![CDATA[<div>Hello <b>World</b></div>]]>", 10, 10, 500, CssData.Parse("body {font-size: 20px}")");<br/>
    /// </para>
    /// <para>
    /// <b>Image rendering</b><br/>
    /// HtmlRender.RenderToImage("<![CDATA[<div>Hello <b>World</b></div>]]>", new Size(600,400));<br/>
    /// HtmlRender.RenderToImage("<![CDATA[<div>Hello <b>World</b></div>]]>", 600);<br/>
    /// HtmlRender.RenderToImage(existingImage, "<![CDATA[<div>Hello <b>World</b></div>]]>");<br/>
    /// </para>
    /// </example>
    public static class HtmlRender
    {
        /// <summary>
        /// Adds a font family to be used in html rendering.<br/>
        /// The added font will be used by all rendering function including <see cref="HtmlContainer"/> and all WinForms controls.
        /// </summary>
        /// <remarks>
        /// The given font family instance must be remain alive while the renderer is in use.<br/>
        /// If loaded to <see cref="PrivateFontCollection"/> then the collection must be alive.<br/>
        /// If loaded from file then the file must not be deleted.
        /// </remarks>
        /// <param name="fontFamily">The font family to add.</param>
        public static void AddFontFamily(FontFamily fontFamily)
        {
            ArgChecker.AssertArgNotNull(fontFamily, "fontFamily");

            WpfAdapter.Instance.AddFontFamily(new FontFamilyAdapter(fontFamily));
        }

        /// <summary>
        /// Adds a font mapping from <paramref name="fromFamily"/> to <paramref name="toFamily"/> iff the <paramref name="fromFamily"/> is not found.<br/>
        /// When the <paramref name="fromFamily"/> font is used in rendered html and is not found in existing 
        /// fonts (installed or added) it will be replaced by <paramref name="toFamily"/>.<br/>
        /// </summary>
        /// <remarks>
        /// This fonts mapping can be used as a fallback in case the requested font is not installed in the client system.
        /// </remarks>
        /// <param name="fromFamily">the font family to replace</param>
        /// <param name="toFamily">the font family to replace with</param>
        public static void AddFontFamilyMapping(string fromFamily, string toFamily)
        {
            ArgChecker.AssertArgNotNullOrEmpty(fromFamily, "fromFamily");
            ArgChecker.AssertArgNotNullOrEmpty(toFamily, "toFamily");

            WpfAdapter.Instance.AddFontFamilyMapping(fromFamily, toFamily);
        }

        /// <summary>
        /// Parse the given stylesheet to <see cref="CssData"/> object.<br/>
        /// If <paramref name="combineWithDefault"/> is true the parsed css blocks are added to the 
        /// default css data (as defined by W3), merged if class name already exists. If false only the data in the given stylesheet is returned.
        /// </summary>
        /// <seealso cref="http://www.w3.org/TR/CSS21/sample.html"/>
        /// <param name="stylesheet">the stylesheet source to parse</param>
        /// <param name="combineWithDefault">true - combine the parsed css data with default css data, false - return only the parsed css data</param>
        /// <returns>the parsed css data</returns>
        public static CssData ParseStyleSheet(string stylesheet, bool combineWithDefault = true)
        {
            return CssData.Parse(WpfAdapter.Instance, stylesheet, combineWithDefault);
        }

        /// <summary>
        /// Measure the size (width and height) required to draw the given html under given max width restriction.<br/>
        /// If no max width restriction is given the layout will use the maximum possible width required by the content,
        /// it can be the longest text line or full image width.<br/>
        /// </summary>
        /// <param name="g">Device to use for measure</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="maxWidth">optional: bound the width of the html to render in (default - 0, unlimited)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the size required for the html</returns>
        public static Size Measure(DrawingContext g, string html, double maxWidth = 0, CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            ArgChecker.AssertArgNotNull(g, "g");
            return MeasureInt(g, html, maxWidth, cssData, stylesheetLoad, imageLoad);
        }

        /// <summary>
        /// Renders the specified HTML source on the specified location and max width restriction.<br/>
        /// If <paramref name="maxWidth"/> is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// Returned is the actual width and height of the rendered html.<br/>
        /// </summary>
        /// <param name="g">Device to render with</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="left">optional: the left most location to start render the html at (default - 0)</param>
        /// <param name="top">optional: the top most location to start render the html at (default - 0)</param>
        /// <param name="maxWidth">optional: bound the width of the html to render in (default - 0, unlimited)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the actual size of the rendered html</returns>
        public static Size Render(DrawingContext g, string html, double left = 0, double top = 0, double maxWidth = 0, CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            ArgChecker.AssertArgNotNull(g, "g");
            return RenderClip(g, html, new Point(left, top), new Size(maxWidth, 0), cssData, stylesheetLoad, imageLoad);
        }

        /// <summary>
        /// Renders the specified HTML source on the specified location and max size restriction.<br/>
        /// If <paramref name="maxSize"/>.Width is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// If <paramref name="maxSize"/>.Height is zero the html will use all the required height, otherwise it will clip at the
        /// given max height not rendering the html below it.<br/>
        /// Returned is the actual width and height of the rendered html.<br/>
        /// </summary>
        /// <param name="g">Device to render with</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="location">the top-left most location to start render the html at</param>
        /// <param name="maxSize">the max size of the rendered html (if height above zero it will be clipped)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the actual size of the rendered html</returns>
        public static Size Render(DrawingContext g, string html, Point location, Size maxSize, CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            ArgChecker.AssertArgNotNull(g, "g");
            return RenderClip(g, html, location, maxSize, cssData, stylesheetLoad, imageLoad);
        }

        /*

        /// <summary>
        /// Renders the specified HTML into a new image of the requested size.<br/>
        /// The HTML will be layout by the given size but will be clipped if cannot fit.<br/>
        /// </summary>
        /// <param name="html">HTML source to render</param>
        /// <param name="size">The size of the image to render into, layout html by width and clipped by height</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the generated image of the html</returns>
        public static BitmapImage RenderToImage(string html, Size size, CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            // create the final image to render into
            var image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

            if (!string.IsNullOrEmpty(html))
            {
                // create memory buffer from desktop handle that supports alpha channel
                IntPtr dib;
                var memoryHdc = Win32Utils.CreateMemoryHdc(IntPtr.Zero, image.Width, image.Height, out dib);
                try
                {
                    // create memory buffer graphics to use for HTML rendering
                    using (var memoryGraphics = Graphics.FromHdc(memoryHdc))
                    {
                        memoryGraphics.Clear(backgroundColor != Color.Empty ? backgroundColor : Color.White);

                        // render HTML into the memory buffer
                        RenderHtml(memoryGraphics, html, PointF.Empty, size, cssData, true, stylesheetLoad, imageLoad);
                    }

                    // copy from memory buffer to image
                    CopyBufferToImage(memoryHdc, image);
                }
                finally
                {
                    Win32Utils.ReleaseMemoryHdc(memoryHdc, dib);
                }
            }

            return image;
        }

        /// <summary>
        /// Renders the specified HTML into a new image of unknown size that will be determined by max width/height and HTML layout.<br/>
        /// If <paramref name="maxWidth"/> is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// If <paramref name="maxHeight"/> is zero the html will use all the required height, otherwise it will clip at the
        /// given max height not rendering the html below it.<br/>
        /// <p>
        /// Limitation: The image cannot have transparent background, by default it will be white.<br/>
        /// See "Rendering to image" remarks section on <see cref="HtmlRender"/>.<br/>
        /// </p>
        /// </summary>
        /// <param name="html">HTML source to render</param>
        /// <param name="maxWidth">optional: the max width of the rendered html, if not zero and html cannot be layout within the limit it will be clipped</param>
        /// <param name="maxHeight">optional: the max height of the rendered html, if not zero and html cannot be layout within the limit it will be clipped</param>
        /// <param name="backgroundColor">optional: the color to fill the image with (default - white)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the generated image of the html</returns>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="backgroundColor"/> is <see cref="Color.Transparent"/></exception>.
        public static BitmapImage RenderToImage(string html, int maxWidth = 0, int maxHeight = 0, Color backgroundColor = new Color(), CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            return RenderToImage(html, Size.Empty, new Size(maxWidth, maxHeight), backgroundColor, cssData, stylesheetLoad, imageLoad);
        }

        /// <summary>
        /// Renders the specified HTML into a new image of unknown size that will be determined by min/max width/height and HTML layout.<br/>
        /// If <paramref name="maxSize.Width"/> is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// If <paramref name="maxSize.Height"/> is zero the html will use all the required height, otherwise it will clip at the
        /// given max height not rendering the html below it.<br/>
        /// If <paramref name="minSize"/> (Width/Height) is above zero the rendered image will not be smaller than the given min size.<br/>
        /// <p>
        /// Limitation: The image cannot have transparent background, by default it will be white.<br/>
        /// See "Rendering to image" remarks section on <see cref="HtmlRender"/>.<br/>
        /// </p>
        /// </summary>
        /// <param name="html">HTML source to render</param>
        /// <param name="minSize">optional: the min size of the rendered html (zero - not limit the width/height)</param>
        /// <param name="maxSize">optional: the max size of the rendered html, if not zero and html cannot be layout within the limit it will be clipped (zero - not limit the width/height)</param>
        /// <param name="backgroundColor">optional: the color to fill the image with (default - white)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the generated image of the html</returns>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="backgroundColor"/> is <see cref="Color.Transparent"/></exception>.
        public static BitmapImage RenderToImage(string html, Size minSize, Size maxSize, Color backgroundColor = new Color(), CssData cssData = null,
            EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad = null, EventHandler<HtmlImageLoadEventArgs> imageLoad = null)
        {
            if (backgroundColor == Color.Transparent)
                throw new ArgumentOutOfRangeException("backgroundColor", "Transparent background in not supported");

            if (string.IsNullOrEmpty(html))
                return new Bitmap(0, 0, PixelFormat.Format32bppArgb);

            using (var container = new HtmlContainer())
            {
                container.AvoidAsyncImagesLoading = true;
                container.AvoidImagesLateLoading = true;

                if (stylesheetLoad != null)
                    container.StylesheetLoad += stylesheetLoad;
                if (imageLoad != null)
                    container.ImageLoad += imageLoad;
                container.SetHtml(html, cssData);

                var finalSize = MeasureHtmlByRestrictions(container, minSize, maxSize);
                container.MaxSize = finalSize;

                // create the final image to render into by measured size
                var image = new Bitmap(finalSize.Width, finalSize.Height, PixelFormat.Format32bppArgb);

                // create memory buffer from desktop handle that supports alpha channel
                IntPtr dib;
                var memoryHdc = Win32Utils.CreateMemoryHdc(IntPtr.Zero, image.Width, image.Height, out dib);
                try
                {
                    // render HTML into the memory buffer
                    using (var memoryGraphics = Graphics.FromHdc(memoryHdc))
                    {
                        memoryGraphics.Clear(backgroundColor != Color.Empty ? backgroundColor : Color.White);
                        container.PerformPaint(memoryGraphics);
                    }

                    // copy from memory buffer to image
                    CopyBufferToImage(memoryHdc, image);
                }
                finally
                {
                    Win32Utils.ReleaseMemoryHdc(memoryHdc, dib);
                }

                return image;
            }
        }
        */

        #region Private methods

        /// <summary>
        /// Measure the size (width and height) required to draw the given html under given width and height restrictions.<br/>
        /// </summary>
        /// <param name="g">Device to use for measure</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="maxWidth">optional: bound the width of the html to render in (default - 0, unlimited)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the size required for the html</returns>
        private static Size MeasureInt(DrawingContext g, string html, double maxWidth, CssData cssData, EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad, EventHandler<HtmlImageLoadEventArgs> imageLoad)
        {
            Size actualSize = Size.Empty;
            if (!string.IsNullOrEmpty(html))
            {
                using (var container = new HtmlContainer())
                {
                    container.MaxSize = new Size(maxWidth, 0);
                    container.AvoidAsyncImagesLoading = true;
                    container.AvoidImagesLateLoading = true;

                    if (stylesheetLoad != null)
                        container.StylesheetLoad += stylesheetLoad;
                    if (imageLoad != null)
                        container.ImageLoad += imageLoad;

                    container.SetHtml(html, cssData);
                    container.PerformLayout(g);

                    actualSize = container.ActualSize;
                }
            }
            return actualSize;
        }

        /*

        /// <summary>
        /// Measure the size of the html by performing layout under the given restrictions.
        /// </summary>
        /// <param name="htmlContainer">the html to calculate the layout for</param>
        /// <param name="minSize">the minimal size of the rendered html (zero - not limit the width/height)</param>
        /// <param name="maxSize">the maximum size of the rendered html, if not zero and html cannot be layout within the limit it will be clipped (zero - not limit the width/height)</param>
        /// <returns>return: the size of the html to be rendered within the min/max limits</returns>
        private static Size MeasureHtmlByRestrictions(HtmlContainer htmlContainer, Size minSize, Size maxSize)
        {
            // use desktop created graphics to measure the HTML
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            using (var mg = new GraphicsAdapter(g))
            {
                var sizeInt = HtmlRendererUtils.MeasureHtmlByRestrictions(mg, htmlContainer.HtmlContainerInt, Utils.Convert(minSize), Utils.Convert(maxSize));
                return Utils.ConvertRound(sizeInt);
            }
        }

         */
          
        /// <summary>
        /// Renders the specified HTML source on the specified location and max size restriction.<br/>
        /// If <paramref name="maxSize"/>.Width is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// If <paramref name="maxSize"/>.Height is zero the html will use all the required height, otherwise it will clip at the
        /// given max height not rendering the html below it.<br/>
        /// Clip the graphics so the html will not be rendered outside the max height bound given.<br/>
        /// Returned is the actual width and height of the rendered html.<br/>
        /// </summary>
        /// <param name="g">Device to render with</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="location">the top-left most location to start render the html at</param>
        /// <param name="maxSize">the max size of the rendered html (if height above zero it will be clipped)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="useGdiPlusTextRendering">true - use GDI+ text rendering, false - use GDI text rendering</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the actual size of the rendered html</returns>
        private static Size RenderClip(DrawingContext g, string html, Point location, Size maxSize, CssData cssData, EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad, EventHandler<HtmlImageLoadEventArgs> imageLoad)
        {
//            Region prevClip = null;
//            if (maxSize.Height > 0)
//            {
//                prevClip = g.Clip;
//                g.SetClip(new RectangleF(location, maxSize));
//            }

            var actualSize = RenderHtml(g, html, location, maxSize, cssData, stylesheetLoad, imageLoad);

//            if (prevClip != null)
//            {
//                g.SetClip(prevClip, CombineMode.Replace);
//            }

            return actualSize;
        }

        /// <summary>
        /// Renders the specified HTML source on the specified location and max size restriction.<br/>
        /// If <paramref name="maxSize"/>.Width is zero the html will use all the required width, otherwise it will perform line 
        /// wrap as specified in the html<br/>
        /// If <paramref name="maxSize"/>.Height is zero the html will use all the required height, otherwise it will clip at the
        /// given max height not rendering the html below it.<br/>
        /// Returned is the actual width and height of the rendered html.<br/>
        /// </summary>
        /// <param name="g">Device to render with</param>
        /// <param name="html">HTML source to render</param>
        /// <param name="location">the top-left most location to start render the html at</param>
        /// <param name="maxSize">the max size of the rendered html (if height above zero it will be clipped)</param>
        /// <param name="cssData">optional: the style to use for html rendering (default - use W3 default style)</param>
        /// <param name="stylesheetLoad">optional: can be used to overwrite stylesheet resolution logic</param>
        /// <param name="imageLoad">optional: can be used to overwrite image resolution logic</param>
        /// <returns>the actual size of the rendered html</returns>
        private static Size RenderHtml(DrawingContext g, string html, Point location, Size maxSize, CssData cssData, EventHandler<HtmlStylesheetLoadEventArgs> stylesheetLoad, EventHandler<HtmlImageLoadEventArgs> imageLoad)
        {
            Size actualSize = Size.Empty;

            if (!string.IsNullOrEmpty(html))
            {
                using (var container = new HtmlContainer())
                {
                    container.Location = location;
                    container.MaxSize = maxSize;
                    container.AvoidAsyncImagesLoading = true;
                    container.AvoidImagesLateLoading = true;

                    if (stylesheetLoad != null)
                        container.StylesheetLoad += stylesheetLoad;
                    if (imageLoad != null)
                        container.ImageLoad += imageLoad;

                    container.SetHtml(html, cssData);
                    container.PerformLayout(g);
                    container.PerformPaint(g);

                    actualSize = container.ActualSize;
                }
            }

            return actualSize;
        }

        #endregion
    }
}