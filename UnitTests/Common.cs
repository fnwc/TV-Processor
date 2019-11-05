using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class Common
    {
        [TestMethod]
        public void ShowAlias()
        {
            var newName1 = ShowAliases.RenameByAlias("The Americans 2013 1080p-YiFY");
            var newName2 = ShowAliases.RenameByAlias("The.Americans.2013.1080p-YiFY");
            var newName3 = ShowAliases.RenameByAlias("The Americans 1080p-YiFY");
            var newName4 = ShowAliases.RenameByAlias("The.Americans.1080p-YiFY");
        }
    }
}
