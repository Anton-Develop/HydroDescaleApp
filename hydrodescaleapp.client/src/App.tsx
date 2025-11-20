import { useState, useEffect } from 'react';
import axios from 'axios';
import {
  Container,
  Typography,
  Box,
  Alert,
  Button,
  ButtonGroup,
  TextField,
  InputAdornment
} from '@mui/material';
import { DataGrid, GridColDef, GridRowId, GridSortModel, GridPaginationModel } from '@mui/x-data-grid';
import SearchIcon from '@mui/icons-material/Search';

interface SteelGrade {
  id: number;
  steelGradeName: string;
  numberOfPumps: number;
  pressureSetting: number;
  createdAt: string;
  updatedAt: string;
}

interface PlcStatus {
  isConnected: boolean;
  lastSuccessfulWrite: string;
  lastErrorMessage: string;
}

const App = () => {
  const [steelGrades, setSteelGrades] = useState<SteelGrade[]>([]);
  const [filteredGrades, setFilteredGrades] = useState<SteelGrade[]>([]);
  const [newGrade, setNewGrade] = useState<Omit<SteelGrade, 'id' | 'createdAt' | 'updatedAt'>>({ steelGradeName: '', numberOfPumps: 2, pressureSetting: 18.3 });
  const [plcStatus, setPlcStatus] = useState<PlcStatus | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [readResult, setReadResult] = useState<{ value: number; error?: string } | null>(null);
  const [writeResult, setWriteResult] = useState<{ success: boolean; error?: string } | null>(null);
  const [syncMessage, setSyncMessage] = useState<string | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [sortModel, setSortModel] = useState<GridSortModel>([]);
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 10,
  });
  const [editRowId, setEditRowId] = useState<GridRowId | null>(null);
  const [editRowData, setEditRowData] = useState<Partial<SteelGrade> | null>(null);

  useEffect(() => {
    fetchSteelGrades();
  }, []);

  const fetchSteelGrades = async () => {
    try {
      const res = await axios.get<SteelGrade[]>('https://localhost:5000/api/steelgrades');
      setSteelGrades(res.data);
    } catch (err) {
      console.error(err);
    }
  };

  // Фильтрация и сортировка
  useEffect(() => {
    let result = steelGrades;

    // Поиск
    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      result = result.filter(grade =>
        grade.steelGradeName.toLowerCase().includes(term) ||
        grade.numberOfPumps.toString().includes(term) ||
        grade.pressureSetting.toString().includes(term)
      );
    }

    // Сортировка
    if (sortModel.length > 0) {
      const sort = sortModel[0];
      result = [...result].sort((a, b) => {
        let valA = a[sort.field as keyof SteelGrade];
        let valB = b[sort.field as keyof SteelGrade];
        if (typeof valA === 'string') {
          valA = (valA as string).toLowerCase();
          valB = (valB as string).toLowerCase();
        }
        if (sort.sort === 'asc') {
          return (valA < valB ? -1 : 1);
        } else {
          return (valA > valB ? -1 : 1);
        }
      });
    }

    setFilteredGrades(result);
  }, [steelGrades, searchTerm, sortModel]);

  const handleSyncSteelGrades = async () => {
    setSyncMessage(null);
    setSyncError(null);
    try {
      const res = await axios.post('https://localhost:5000/api/sync/load-steel-grades');
      setSyncMessage(res.data.message);
      fetchSteelGrades();
    } catch (err: any) {
      setSyncError(err.response?.data?.error || 'Failed to sync steel grades');
    }
  };

  const handleAdd = async () => {
    try {
      await axios.post('https://localhost:5000/api/steelgrades', newGrade);
      fetchSteelGrades();
      setNewGrade({ steelGradeName: '', numberOfPumps: 2, pressureSetting: 18.3 });
    } catch (err) {
      console.error(err);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await axios.delete(`https://localhost:5000/api/steelgrades/${id}`);
      fetchSteelGrades();
    } catch (err) {
      console.error(err);
    }
  };

  const handleEditClick = (id: GridRowId) => {
    const row = steelGrades.find(g => g.id === id);
    if (row) {
      setEditRowId(id);
      setEditRowData({ id: row.id, numberOfPumps: row.numberOfPumps, pressureSetting: row.pressureSetting });
    }
  };

  const handleEditChange = (field: keyof SteelGrade, value: any) => {
    setEditRowData(prev => ({ ...prev, [field]: value }));
  };

  const handleSaveEdit = async () => {
    if (editRowData) {
      try {
        await axios.put(`https://localhost:5000/api/steelgrades/${editRowData.id}`, {
          steelGradeName: steelGrades.find(g => g.id === editRowData.id)?.steelGradeName,
          numberOfPumps: editRowData.numberOfPumps,
          pressureSetting: editRowData.pressureSetting
        });
        fetchSteelGrades();
        setEditRowId(null);
        setEditRowData(null);
      } catch (err) {
        console.error('Error updating grade:', err);
      }
    }
  };

  const handleCancelEdit = () => {
    setEditRowId(null);
    setEditRowData(null);
  };

  const columns: GridColDef[] = [
    { field: 'steelGradeName', headerName: 'Марка стали', flex: 1, sortable: true },
    {
      field: 'numberOfPumps',
      headerName: 'Насосы',
      type: 'number',
      flex: 0.5,
      sortable: true,
      renderCell: (params) => {
        if (params.id === editRowId) {
          return (
            <select
              value={editRowData?.numberOfPumps ?? params.value}
              onChange={(e) => handleEditChange('numberOfPumps', parseInt(e.target.value))}
              style={{ width: '100%', padding: '4px' }}
            >
              <option value={1}>1</option>
              <option value={2}>2</option>
              <option value={3}>3</option>
              <option value={4}>4</option>
            </select>
          );
        }
        return params.value;
      }
    },
    {
      field: 'pressureSetting',
      headerName: 'Давление (MPa)',
      type: 'number',
      flex: 0.7,
      sortable: true,
      renderCell: (params) => {
        if (params.id === editRowId) {
          return (
            <input
              type="number"
              value={editRowData?.pressureSetting ?? params.value}
              onChange={(e) => handleEditChange('pressureSetting', parseFloat(e.target.value) || 0)}
              step="0.1"
              style={{ width: '100%', padding: '4px' }}
            />
          );
        }
        return params.value?.toFixed(2);
      }
    },
    {
      field: 'actions',
      headerName: 'Действия',
      type: 'actions',
      flex: 0.5,
      renderCell: (params) => (
        <>
          {editRowId === params.id ? (
            <>
              <Button size="small" color="primary" onClick={handleSaveEdit}>Сохранить</Button>
              <Button size="small" color="secondary" onClick={handleCancelEdit}>Отмена</Button>
            </>
          ) : (
            <>
              <Button size="small" color="primary" onClick={() => handleEditClick(params.id)}>Редактировать</Button>
              <Button size="small" color="error" onClick={() => handleDelete(params.row.id)}>Удалить</Button>
            </>
          )}
        </>
      )
    }
  ];

  return (
    <Container>
      <Typography variant="h4" gutterBottom>Сервис для работы гидросбива</Typography>

      <Box my={2}>
        <Button variant="outlined" onClick={handleSyncSteelGrades}>
          Загрузить марки стали из БД
        </Button>
      </Box>

      {syncMessage && (
        <Alert severity="info" style={{ marginBottom: '16px' }}>
          {syncMessage}
        </Alert>
      )}
      {syncError && (
        <Alert severity="error" style={{ marginBottom: '16px' }}>
          {syncError}
        </Alert>
      )}

      <Box my={2}>
        <TextField
          label="Поиск"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          size="small"
          fullWidth
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon />
              </InputAdornment>
            ),
          }}
        />
      </Box>

      <Box my={2}>
        <TextField
          label="Марка стали"
          value={newGrade.steelGradeName}
          onChange={(e) => setNewGrade({ ...newGrade, steelGradeName: e.target.value })}
          size="small"
          sx={{ mr: 1, width: '30%' }}
        />
        <TextField
          label="Насосы"
          type="number"
          value={newGrade.numberOfPumps}
          onChange={(e) => setNewGrade({ ...newGrade, numberOfPumps: parseInt(e.target.value) || 2 })}
          size="small"
          sx={{ mr: 1, width: '15%' }}
        />
        <TextField
          label="Давление"
          type="number"
          value={newGrade.pressureSetting}
          onChange={(e) => setNewGrade({ ...newGrade, pressureSetting: parseFloat(e.target.value) || 18.3 })}
          inputProps={{ step: 0.1 }}
          size="small"
          sx={{ mr: 1, width: '15%' }}
        />
        <Button variant="contained" onClick={handleAdd}>Add</Button>
      </Box>

      <Box my={2}>
        <DataGrid
          rows={filteredGrades}
          columns={columns}
          paginationMode="client"
          paginationModel={paginationModel}
          onPaginationModelChange={setPaginationModel}
          sortModel={sortModel}
          onSortModelChange={setSortModel}
          autoHeight
          density="compact"
          pageSizeOptions={[5, 10, 20]}
        />
      </Box>
    </Container>
  );
};

export default App;