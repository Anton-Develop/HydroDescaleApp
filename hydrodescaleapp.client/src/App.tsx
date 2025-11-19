import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { Container, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Button, TextField, Box, Alert } from '@mui/material';
import { SteelGrade } from './types';
import { Alert, ButtonGroup } from '@mui/material';

interface PlcStatus {
  isConnected: boolean;
  lastSuccessfulWrite: string;
  lastErrorMessage: string;
}

const App = () => {
  const [steelGrades, setSteelGrades] = useState<SteelGrade[]>([]);
  const [newGrade, setNewGrade] = useState<Omit<SteelGrade, 'id'>>({ SteelGradeName: '', NumberOfPumps: 2, PressureSetting: 18.3 });
  const [plcStatus, setPlcStatus] = useState<PlcStatus | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [readResult, setReadResult] = useState<{ value: number; error?: string } | null>(null);
  const [writeResult, setWriteResult] = useState<{ success: boolean; error?: string } | null>(null);

  const handleReadFurnace = async () => {
    try {
      const res = await axios.get('http://localhost:5000/api/diagnostic/read-furnace');
      setReadResult(res.data);
      setWriteResult(null);
    } catch (err: any) {
      setReadResult({ error: err.response?.data?.error || 'Unknown error' });
    }
  };

  const handleWriteTest = async () => {
    try {
      const res = await axios.post('http://localhost:5000/api/diagnostic/write-test', {
        pumps: 2,
        pressure: 18.3
      });
      setWriteResult(res.data);
      setReadResult(null);
    } catch (err: any) {
      setWriteResult({ success: false, error: err.response?.data?.error || 'Unknown error' });
    }
  };

  useEffect(() => {
    fetchSteelGrades();
    const interval = setInterval(() => {
      fetchPlcStatus();
    }, 5000); // Обновляем статус PLC каждые 5 секунд

    return () => clearInterval(interval);
  }, []);

  const fetchSteelGrades = async () => {
    try {
      const res = await axios.get<SteelGrade[]>('http://localhost:5000/api/steelgrades');
      setSteelGrades(res.data);
    } catch (err) {
      console.error(err);
    }
  };

  const fetchPlcStatus = async () => {
    try {
      const res = await axios.get<PlcStatus>('http://localhost:5000/api/plcstatus');
      setPlcStatus(res.data);
      setStatusError(null);
    } catch (err) {
      setStatusError('Failed to fetch PLC status');
      console.error(err);
    }
  };

  const handleAdd = async () => {
    try {
      await axios.post('http://localhost:5000/api/steelgrades', newGrade);
      fetchSteelGrades();
      setNewGrade({ SteelGradeName: '', NumberOfPumps: 2, PressureSetting: 18.3 });
    } catch (err) {
      console.error(err);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await axios.delete(`http://localhost:5000/api/steelgrades/${id}`);
      fetchSteelGrades();
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <Container>
      <Typography variant="h4" gutterBottom>Сервис для работы гидросбива</Typography>
      {/* Кнопки диагностики */}
      <Box my={2}>
        <ButtonGroup variant="contained" aria-label="Diagnostic buttons">
          <Button onClick={handleReadFurnace}>Read Furnace Number</Button>
          <Button onClick={handleWriteTest}>Write Test Values</Button>
        </ButtonGroup>
      </Box>

      {/* Результаты диагностики */}
      {readResult && (
        <Alert severity={readResult.error ? "error" : "info"} style={{ marginBottom: '16px' }}>
          Read Result: {readResult.error ? `Error: ${readResult.error}` : `Value: ${readResult.value}`}
        </Alert>
      )}
      {writeResult && (
        <Alert severity={writeResult.success ? "success" : "error"} style={{ marginBottom: '16px' }}>
          Write Result: {writeResult.success ? 'Success' : `Error: ${writeResult.error}`}
        </Alert>
      )}
      
      {/* Статус PLC */}
      {plcStatus && (
        <Alert severity={plcStatus.isConnected ? "success" : "error"} style={{ marginBottom: '16px' }}>
          PLC Status: {plcStatus.isConnected ? 'Connected' : 'Disconnected'}.
          {plcStatus.isConnected ? (
            <span> Last write: {new Date(plcStatus.lastSuccessfulWrite).toLocaleString()}</span>
          ) : (
            <span> Error: {plcStatus.lastErrorMessage}</span>
          )}
        </Alert>
      )}
      {statusError && (
        <Alert severity="error" style={{ marginBottom: '16px' }}>
          {statusError}
        </Alert>
      )}

      <Box my={2}>
        <TextField
          label="Steel Grade"
          value={newGrade.SteelGradeName}
          onChange={(e) => setNewGrade({ ...newGrade, SteelGradeName: e.target.value })}
          size="small"
          sx={{ mr: 1 }}
        />
        <TextField
          label="Pumps"
          type="number"
          value={newGrade.NumberOfPumps}
          onChange={(e) => setNewGrade({ ...newGrade, NumberOfPumps: parseInt(e.target.value) || 2 })}
          size="small"
          sx={{ mr: 1 }}
        />
        <TextField
          label="Pressure"
          type="number"
          step="0.1"
          value={newGrade.PressureSetting}
          onChange={(e) => setNewGrade({ ...newGrade, PressureSetting: parseFloat(e.target.value) || 18.3 })}
          size="small"
          sx={{ mr: 1 }}
        />
        <Button variant="contained" onClick={handleAdd}>Add</Button>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Grade</TableCell>
              <TableCell align="right">Pumps</TableCell>
              <TableCell align="right">Pressure (MPa)</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {steelGrades.map((grade) => (
              <TableRow key={grade.Id}>
                <TableCell>{grade.SteelGradeName}</TableCell>
                <TableCell align="right">{grade.NumberOfPumps}</TableCell>
                <TableCell align="right">{grade.PressureSetting.toFixed(2)}</TableCell>
                <TableCell align="right">
                  <Button size="small" color="error" onClick={() => handleDelete(grade.Id)}>Delete</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Container>
  );
};

export default App;