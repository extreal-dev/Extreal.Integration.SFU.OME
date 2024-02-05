namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Class that represents the group.
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new group.
        /// </summary>
        /// <param name="name">Name.</param>
        public Group(string name) => Name = name;
    }
}
