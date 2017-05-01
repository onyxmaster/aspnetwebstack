// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Security;
using System.Text;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.WebPages.Test
{
    public class WebPageHttpHandlerTest
    {
        [Fact]
        public void ConstructorThrowsWithNullPage()
        {
            Assert.ThrowsArgumentNull(() => new WebPageHttpHandler(null), "webPage");
        }

        [Fact]
        public void IsReusableTest()
        {
            WebPage dummyPage = new DummyPage();
            Assert.False(new WebPageHttpHandler(dummyPage).IsReusable);
        }

        [Fact]
        public void ProcessRequestTest()
        {
            var contents = "test";
            var tw = new StringWriter(new StringBuilder());
            var httpContext = CreateTestContext(tw);
            var page = Utils.CreatePage(p => p.Write(contents));
            new WebPageHttpHandler(page).ProcessRequestInternal(httpContext);
            Assert.Equal(contents, tw.ToString());
        }

        [Fact]
        public void ExceptionTest()
        {
            var contents = "test";
            var httpContext = Utils.CreateTestContext().Object;
            var page = Utils.CreatePage(p => { throw new InvalidOperationException(contents); });
            var e = Assert.Throws<HttpUnhandledException>(
                () => new WebPageHttpHandler(page).ProcessRequestInternal(httpContext)
            );
            Assert.IsType<InvalidOperationException>(e.InnerException);
            Assert.Equal(contents, e.InnerException.Message, StringComparer.Ordinal);
        }

        [Fact]
        public void SecurityExceptionTest()
        {
            var contents = "test";
            var httpContext = Utils.CreateTestContext().Object;
            var page = Utils.CreatePage(p => { throw new SecurityException(contents); });
            Assert.Throws<SecurityException>(
                () => new WebPageHttpHandler(page).ProcessRequestInternal(httpContext),
                contents);
        }

        [Fact]
        public void CreateFromVirtualPathTest()
        {
            var contents = "test";
            var textWriter = new StringWriter();

            var httpResponse = new Mock<HttpResponseBase>();
            httpResponse.SetupGet(r => r.Output).Returns(textWriter);
            var httpContext = Utils.CreateTestContext(response: httpResponse.Object);
            var mockBuildManager = new Mock<IVirtualPathFactory>();
            var virtualPath = "~/hello/test.cshtml";
            var page = Utils.CreatePage(p => p.Write(contents));
            mockBuildManager.Setup(c => c.Exists(It.Is<string>(p => p.Equals(virtualPath)))).Returns<string>(_ => true).Verifiable();
            mockBuildManager.Setup(c => c.CreateInstance(It.Is<string>(p => p.Equals(virtualPath)))).Returns(page).Verifiable();

            // Act
            IHttpHandler handler = WebPageHttpHandler.CreateFromVirtualPath(virtualPath, new VirtualPathFactoryManager(mockBuildManager.Object));
            WebPageHttpHandler webPageHttpHandler = Assert.IsType<WebPageHttpHandler>(handler);
            webPageHttpHandler.ProcessRequestInternal(httpContext.Object);

            // Assert
            Assert.Equal(contents, textWriter.ToString());
            mockBuildManager.Verify();
        }

        [Fact]
        public void VersionHeaderTest()
        {
            Mock<HttpResponseBase> mockResponse = new Mock<HttpResponseBase>();
            mockResponse.Setup(response => response.AppendHeader("X-AspNetWebPages-Version", LatestRazorVersion.MajorMinor)).Verifiable();

            Mock<HttpContextBase> mockContext = new Mock<HttpContextBase>();
            mockContext.SetupGet(context => context.Response).Returns(mockResponse.Object);

            WebPageHttpHandler.AddVersionHeader(mockContext.Object);
            mockResponse.Verify();
        }

        [Fact]
        public void CreateFromVirtualPathNonWebPageTest()
        {
            // Arrange
            var virtualPath = "~/hello/test.cshtml";
            var handler = new WebPageHttpHandler(new DummyPage());
            var mockBuildManager = new Mock<IVirtualPathFactory>();
            mockBuildManager.Setup(c => c.CreateInstance(It.IsAny<string>())).Returns(handler);
            mockBuildManager.Setup(c => c.Exists(It.Is<string>(p => p.Equals(virtualPath)))).Returns<string>(_ => true).Verifiable();

            // Act
            var result = WebPageHttpHandler.CreateFromVirtualPath(virtualPath, new VirtualPathFactoryManager(mockBuildManager.Object));

            // Assert
            Assert.Equal(handler, result);
            mockBuildManager.Verify();
        }

        [Fact]
        public void CreateFromVirtualPathReturnsIHttpHandlerIfItCannotCreateAWebPageType()
        {
            // Arrange
            var pageVirtualPath = "~/hello/test.cshtml";
            var handlerVirtualPath = "~/handler.asmx";
            var page = new DummyPage();
            var handler = new Mock<IHttpHandler>().Object;
            var mockFactory = new Mock<IVirtualPathFactory>();
            mockFactory.Setup(c => c.Exists(It.IsAny<string>())).Returns(true);
            mockFactory.Setup(c => c.CreateInstance(pageVirtualPath)).Returns(page);
            mockFactory.Setup(c => c.CreateInstance(handlerVirtualPath)).Returns(handler);

            // Act
            var handlerResult = WebPageHttpHandler.CreateFromVirtualPath(handlerVirtualPath, mockFactory.Object);
            var pageResult = WebPageHttpHandler.CreateFromVirtualPath(pageVirtualPath, mockFactory.Object);

            // Assert
            Assert.Equal(handler, handlerResult);
            Assert.NotNull(pageResult as WebPageHttpHandler);
        }

        private static HttpContextBase CreateTestContext(TextWriter textWriter)
        {
            var filename = "default.aspx";
            var url = "http://localhost/WebSite1/subfolder1/default.aspx";
            var request = Utils.CreateTestRequest(filename, url);

            var response = new Mock<HttpResponseBase>();
            response.SetupGet(r => r.Output).Returns(textWriter);

            return Utils.CreateTestContext(request: request.Object, response: response.Object).Object;
        }

        private sealed class DummyPage : WebPage
        {
            public override void Execute()
            {
            }
        }
    }
}