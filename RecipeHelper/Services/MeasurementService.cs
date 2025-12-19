using Microsoft.EntityFrameworkCore;

namespace RecipeHelper.Services
{
    public class MeasurementService
    {
        DatabaseContext _context;
        ILogger<MeasurementService> _logger;
        public MeasurementService(ILogger<MeasurementService> logger, DatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<List<Measurement>> GetAllMeasurementsAsync()
        {
            return await _context.Measurements.ToListAsync();
        }
    }
}
