// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.WebPages.Test
{
    public class WebPageExecutingBaseTest
    {
        [Fact]
        public void NormalizeLayoutPageUsesVirtualPathFactoryManagerToDetermineIfLayoutFileExists()
        {
            // Arrange
            var layoutPagePath = "~/sitelayout.cshtml";
            var layoutPage = Utils.CreatePage(null, layoutPagePath);
            var page = Utils.CreatePage(null);
            var objectFactory = new Mock<IVirtualPathFactory>();
            objectFactory.Setup(c => c.Exists(It.Is<string>(p => p.Equals(layoutPagePath)))).Returns(true).Verifiable();
            page.VirtualPathFactory = objectFactory.Object;

            // Act
            var path = page.NormalizeLayoutPagePath(layoutPage.VirtualPath);

            // Assert
            objectFactory.Verify();
            Assert.Equal(path, layoutPage.VirtualPath);
        }

        [Fact]
        public void NormalizeLayoutPageAcceptsRelativePathsToLayoutPage()
        {
            // Arrange
            var page = Utils.CreatePage(null, "~/dir/default.cshtml");
            var layoutPage = Utils.CreatePage(null, "~/layouts/sitelayout.cshtml");
            var objectFactory = new HashVirtualPathFactory(page, layoutPage);
            page.VirtualPathFactory = objectFactory;

            // Act
            var path = page.NormalizeLayoutPagePath(@"../layouts/sitelayout.cshtml");

            // Assert
            Assert.Equal(path, layoutPage.VirtualPath);
        }

        [Fact]
        public void WriteAttributeToWritesAttributeNormallyIfNoValuesSpecified()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                expected: " alt=\"\"");
        }

        [Fact]
        public void WriteAttributeToWritesNothingIfSingleNullValueProvided()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                values: new[] {
                    new AttributeValue(String.Empty, null, literal: true)
                },
                expected: "");
        }

        [Fact]
        public void WriteAttributeToWritesNothingIfSingleFalseValueProvided()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                values: new[] {
                    new AttributeValue(String.Empty, (object)false, literal: true)
                },
                expected: "");
        }

        [Fact]
        public void WriteAttributeToWritesGlobalPrefixIfSingleValueProvided()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                values: new[] {
                    new AttributeValue("    ", (object)"foo", literal: true)
                },
                expected: " alt=\"foo\"");
        }

        [Fact]
        public void WriteAttributeToWritesLocalPrefixForSecondValueProvided()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                values: new[] {
                    new AttributeValue("    ", (object)"foo", literal: true),
                    new AttributeValue("glorb", (object)"bar", literal: true)
                },
                expected: " alt=\"fooglorbbar\"");
        }

        [Fact]
        public void WriteAttributeToWritesGlobalPrefixOnlyIfSecondValueIsFirstNonNullOrFalse()
        {
            WriteAttributeTest(
                name: "alt",
                prefix: " alt=\"",
                suffix: "\"",
                values: new[] {
                    new AttributeValue("    ", null, literal: true),
                    new AttributeValue("glorb", (object)"bar", literal: true)
                },
                expected: " alt=\"bar\"");
        }

        /// <remarks>
        /// This is a regression test for Html.Raw behaving incorrectly in attributes - the code here is derived from that generated
        /// by the Razor engine on input like the following:
        /// 
        /// cool="@Html.Raw("this is cool text")"
        /// </remarks>
        [Fact]
        public void WriteAttributeWithRawHtmlString()
        {
            string alreadyEncoded = "Show Size 6½-8";
            WriteAttributeTest(
                name: "alt",
                prefix: " cool=\"",
                suffix: "\"",
                values: new[] {
                    AttributeValue.FromTuple(Tuple.Create("", (object)new HtmlString(alreadyEncoded), false)), 
                },
                expected: " cool=\"" + alreadyEncoded + "\"");
        }

        private void WriteAttributeTest(string name, string prefix, string suffix, string expected)
        {
            WriteAttributeTest(name, prefix, suffix, new AttributeValue[0], expected);
        }

        private void WriteAttributeTest(string name, string prefix, string suffix, AttributeValue[] values, string expected)
        {
            // Arrange
            var pageMock = new Mock<WebPageExecutingBase>() { CallBase = true };
            pageMock.Setup(p => p.Context).Returns(new Mock<HttpContextBase>().Object);

            StringBuilder written = new StringBuilder();
            StringWriter writer = new StringWriter(written);

            // Act
            pageMock.Object.WriteAttributeTo(writer, name, prefix, suffix, values);

            // Assert
            Assert.Equal(expected, written.ToString());
        }
    }
}
