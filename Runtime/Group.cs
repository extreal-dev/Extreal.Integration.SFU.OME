namespace Extreal.Integration.SFU.OME
{
    /// <summary>
    /// Class that represents the group.
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new group.
        /// </summary>
        /// <param name="id">Id.</param>
        /// <param name="name">Name.</param>
        public Group(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
