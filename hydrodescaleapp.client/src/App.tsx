import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { Container, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Button, TextField, Box } from '@mui/material';
import { SteelGrade } from './types';

const App = () => {
  const [steelGrades, setSteelGrades] = useState<SteelGrade[]>([]);
  const [newGrade, setNewGrade] = useState<Omit<SteelGrade, 'id'>>({ SteelGradeName: '', NumberOfPumps: 2, PressureSetting: 18.3 });

  useEffect(() => {
    fetchSteelGrades();
  }, []);

  const fetchSteelGrades = async () => {
    try {
      const res = await axios.get<SteelGrade[]>('http://localhost:5000/api/steelgrades');
      setSteelGrades(res.data);
    } catch (err) {
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
      <Typography variant="h4" gutterBottom>Steel Grades Management</Typography>

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
