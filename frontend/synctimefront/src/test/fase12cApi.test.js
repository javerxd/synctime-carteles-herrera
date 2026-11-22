import { api } from '../api/cliente';
import { publico } from '../api/publico';

vi.mock('../api/cliente', () => ({ api: { get: vi.fn() } }));

const t = (id) => ({ idCatalogo: id, titulo: `T${id}` });

describe('publico.catalogoCompleto', () => {
  beforeEach(() => vi.clearAllMocks());

  it('pide todas las páginas de 100, deduplica y pasa la señal de cancelación', async () => {
    api.get
      .mockResolvedValueOnce({ elementos: [t(1), t(2)], totalPaginas: 2 })
      .mockResolvedValueOnce({ elementos: [t(2), t(3)], totalPaginas: 2 });
    const senal = new AbortController().signal;
    const todos = await publico.catalogoCompleto(senal);
    expect(todos.map((x) => x.idCatalogo)).toEqual([1, 2, 3]);
    expect(api.get).toHaveBeenNthCalledWith(2, '/catalogo', { parametros: { pagina: 2, tamano: 100 }, senal });
  });

  it('se detiene a las 20 páginas y propaga el fallo de una página', async () => {
    api.get.mockResolvedValue({ elementos: [], totalPaginas: 99 });
    await publico.catalogoCompleto();
    expect(api.get).toHaveBeenCalledTimes(20);
    api.get.mockReset().mockResolvedValueOnce({ elementos: [t(1)], totalPaginas: 3 }).mockRejectedValueOnce(new Error('x'));
    await expect(publico.catalogoCompleto()).rejects.toThrow('x');
  });
});
