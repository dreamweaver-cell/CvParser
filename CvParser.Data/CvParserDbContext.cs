namespace CvParser.Data;

public class CvParserDbContext(DbContextOptions<CvParserDbContext> options) : IdentityDbContext<User>(options)
{

}

