import React, { useState } from 'react';

interface TaskProposal {
  title: string;
  description: string;
  priority: string;
  suggestedDueDate: string;
  assignedTo: string;
}

interface TaskProposalCardProps {
  proposals: TaskProposal[];
  onCreateTasks: (tasks: TaskProposal[]) => void;
}

const TaskProposalCard: React.FC<TaskProposalCardProps> = ({ proposals, onCreateTasks }) => {
  const [editedTasks, setEditedTasks] = useState<TaskProposal[]>(proposals);
  const [selectedTasks, setSelectedTasks] = useState<Set<number>>(new Set(proposals.map((_, index) => index)));

  const updateTask = (index: number, field: keyof TaskProposal, value: string) => {
    const updated = [...editedTasks];
    updated[index] = { ...updated[index], [field]: value };
    setEditedTasks(updated);
  };

  const toggleTaskSelection = (index: number) => {
    const newSelected = new Set(selectedTasks);
    if (newSelected.has(index)) {
      newSelected.delete(index);
    } else {
      newSelected.add(index);
    }
    setSelectedTasks(newSelected);
  };

  const selectAll = () => {
    setSelectedTasks(new Set(editedTasks.map((_, index) => index)));
  };

  const selectNone = () => {
    setSelectedTasks(new Set());
  };

  const handleCreateTasks = () => {
    const tasksToCreate = editedTasks.filter((_, index) => selectedTasks.has(index));
    onCreateTasks(tasksToCreate);
  };

  const getPriorityColor = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'high':
        return { bg: '#ef444420', color: '#dc2626', border: '#ef4444' };
      case 'low':
        return { bg: '#10b98120', color: '#059669', border: '#10b981' };
      default:
        return { bg: '#f59e0b20', color: '#d97706', border: '#f59e0b' };
    }
  };

  return (
    <div className="w-full max-w-4xl mx-auto">
      {/* Header */}
      <div 
        className="mb-4 p-4 rounded-lg"
        style={{
          background: 'var(--bg-secondary)',
          border: '1px solid var(--border-primary)'
        }}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div 
              className="w-10 h-10 rounded-lg flex items-center justify-center"
              style={{ background: 'var(--accent-primary)' }}
            >
              <svg className="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
              </svg>
            </div>
            <div>
              <h3 
                className="text-lg font-bold"
                style={{ color: 'var(--text-primary)' }}
              >
                🤖 AI-Proposed Tasks
              </h3>
              <p 
                className="text-sm"
                style={{ color: 'var(--text-secondary)' }}
              >
                {editedTasks.length} task{editedTasks.length !== 1 ? 's' : ''} proposed • Review and customize before creating
              </p>
            </div>
          </div>
          
          {/* Bulk Actions */}
          <div className="flex items-center space-x-2">
            <button
              onClick={selectAll}
              className="text-xs px-2 py-1 rounded transition-colors"
              style={{ 
                color: 'var(--accent-primary)',
                background: 'var(--accent-primary)10'
              }}
              onMouseEnter={(e) => e.currentTarget.style.background = 'var(--accent-primary)20'}
              onMouseLeave={(e) => e.currentTarget.style.background = 'var(--accent-primary)10'}
            >
              Select All
            </button>
            <button
              onClick={selectNone}
              className="text-xs px-2 py-1 rounded transition-colors"
              style={{ 
                color: 'var(--text-secondary)',
                background: 'var(--bg-tertiary)'
              }}
              onMouseEnter={(e) => e.currentTarget.style.background = 'var(--border-primary)'}
              onMouseLeave={(e) => e.currentTarget.style.background = 'var(--bg-tertiary)'}
            >
              Select None
            </button>
          </div>
        </div>
      </div>

      {/* Tasks List */}
      <div className="space-y-3 mb-4">
        {editedTasks.map((task, index) => {
          const isSelected = selectedTasks.has(index);
          const priorityStyle = getPriorityColor(task.priority);
          
          return (
            <div
              key={index}
              className={`rounded-xl shadow-sm transition-all duration-200 ${isSelected ? 'ring-2' : ''}`}
              style={{
                background: 'var(--card-bg)',
                border: '1px solid var(--border-primary)',
                ...(isSelected && { '--tw-ring-color': 'var(--accent-primary)' })
              }}
            >
              <div className="p-4">
                {/* Task Header */}
                <div className="flex items-start space-x-3 mb-3">
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() => toggleTaskSelection(index)}
                    className="mt-1 w-4 h-4 rounded border-2 transition-colors"
                    style={{ 
                      accentColor: 'var(--accent-primary)',
                      borderColor: 'var(--border-primary)'
                    }}
                  />
                  <div className="flex-1 space-y-3">
                    {/* Title */}
                    <div>
                      <label 
                        className="block text-sm font-medium mb-1"
                        style={{ color: 'var(--text-primary)' }}
                      >
                        Task Title
                      </label>
                      <input
                        type="text"
                        value={task.title}
                        onChange={(e) => updateTask(index, 'title', e.target.value)}
                        className="w-full px-3 py-2 rounded-lg text-sm transition-colors"
                        style={{
                          background: 'var(--input-bg)',
                          border: '1px solid var(--border-primary)',
                          color: 'var(--text-primary)'
                        }}
                        placeholder="Enter task title..."
                      />
                    </div>

                    {/* Description */}
                    <div>
                      <label 
                        className="block text-sm font-medium mb-1"
                        style={{ color: 'var(--text-primary)' }}
                      >
                        Description
                      </label>
                      <textarea
                        value={task.description}
                        onChange={(e) => updateTask(index, 'description', e.target.value)}
                        rows={3}
                        className="w-full px-3 py-2 rounded-lg text-sm transition-colors resize-none"
                        style={{
                          background: 'var(--input-bg)',
                          border: '1px solid var(--border-primary)',
                          color: 'var(--text-primary)'
                        }}
                        placeholder="Enter task description..."
                      />
                    </div>

                    {/* Priority and Due Date */}
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                      <div>
                        <label 
                          className="block text-sm font-medium mb-1"
                          style={{ color: 'var(--text-primary)' }}
                        >
                          Priority
                        </label>
                        <select
                          value={task.priority}
                          onChange={(e) => updateTask(index, 'priority', e.target.value)}
                          className="w-full px-3 py-2 rounded-lg text-sm transition-colors"
                          style={{
                            background: 'var(--input-bg)',
                            border: '1px solid var(--border-primary)',
                            color: 'var(--text-primary)'
                          }}
                        >
                          <option value="High">High</option>
                          <option value="Medium">Medium</option>
                          <option value="Low">Low</option>
                        </select>
                      </div>

                      <div>
                        <label 
                          className="block text-sm font-medium mb-1"
                          style={{ color: 'var(--text-primary)' }}
                        >
                          Due Date
                        </label>
                        <input
                          type="date"
                          value={task.suggestedDueDate}
                          onChange={(e) => updateTask(index, 'suggestedDueDate', e.target.value)}
                          className="w-full px-3 py-2 rounded-lg text-sm transition-colors"
                          style={{
                            background: 'var(--input-bg)',
                            border: '1px solid var(--border-primary)',
                            color: 'var(--text-primary)'
                          }}
                        />
                      </div>

                      <div>
                        <label 
                          className="block text-sm font-medium mb-1"
                          style={{ color: 'var(--text-primary)' }}
                        >
                          Assigned To
                        </label>
                        <input
                          type="text"
                          value={task.assignedTo}
                          onChange={(e) => updateTask(index, 'assignedTo', e.target.value)}
                          className="w-full px-3 py-2 rounded-lg text-sm transition-colors"
                          style={{
                            background: 'var(--input-bg)',
                            border: '1px solid var(--border-primary)',
                            color: 'var(--text-primary)'
                          }}
                          placeholder="Unassigned"
                        />
                      </div>
                    </div>

                    {/* Priority Badge */}
                    <div className="flex items-center space-x-2">
                      <span 
                        className="inline-flex items-center px-2 py-1 rounded-md text-xs font-medium"
                        style={{ 
                          background: priorityStyle.bg,
                          color: priorityStyle.color,
                          border: `1px solid ${priorityStyle.border}`
                        }}
                      >
                        {task.priority} Priority
                      </span>
                      {task.suggestedDueDate && (
                        <span 
                          className="inline-flex items-center px-2 py-1 rounded-md text-xs font-medium"
                          style={{ 
                            background: 'var(--bg-tertiary)',
                            color: 'var(--text-secondary)'
                          }}
                        >
                          📅 Due: {new Date(task.suggestedDueDate).toLocaleDateString()}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Create Tasks Button */}
      {selectedTasks.size > 0 && (
        <div 
          className="p-4 rounded-lg"
          style={{
            background: 'var(--bg-secondary)',
            border: '1px solid var(--border-primary)'
          }}
        >
          <div className="flex items-center justify-between">
            <div>
              <p 
                className="text-sm font-medium"
                style={{ color: 'var(--text-primary)' }}
              >
                Ready to create {selectedTasks.size} task{selectedTasks.size !== 1 ? 's' : ''}
              </p>
              <p 
                className="text-xs"
                style={{ color: 'var(--text-secondary)' }}
              >
                Tasks will be added to your Microsoft To Do list
              </p>
            </div>
            <button
              onClick={handleCreateTasks}
              className="inline-flex items-center px-4 py-2 rounded-lg text-sm font-medium text-white transition-colors"
              style={{ background: 'var(--accent-primary)' }}
              onMouseEnter={(e) => e.currentTarget.style.background = 'var(--accent-secondary)'}
              onMouseLeave={(e) => e.currentTarget.style.background = 'var(--accent-primary)'}
            >
              <svg className="w-4 h-4 mr-2" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
              </svg>
              Create {selectedTasks.size} Task{selectedTasks.size !== 1 ? 's' : ''}
            </button>
          </div>
        </div>
      )}

      {selectedTasks.size === 0 && (
        <div 
          className="p-4 rounded-lg text-center"
          style={{
            background: 'var(--bg-secondary)',
            border: '1px solid var(--border-primary)'
          }}
        >
          <p 
            className="text-sm"
            style={{ color: 'var(--text-secondary)' }}
          >
            Select tasks to create them in Microsoft To Do
          </p>
        </div>
      )}
    </div>
  );
};

export default TaskProposalCard; 